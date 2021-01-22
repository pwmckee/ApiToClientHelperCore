using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;

namespace ApiToClientHelperCore
{
    public static class TypeScriptInterfacesExtension
    {
        private const string ImportExportTemplate = "{0} {{{1}}} from \"./{1}\";";
        private static string _inputNamespace;

        private static Type[] nonPrimitivesExcludeList = new Type[]
        {
            typeof(object),
            typeof(string),
            typeof(decimal),
            typeof(void),
            typeof(Guid)
        };

        private static IDictionary<Type, string> convertedTypes = new Dictionary<Type, string>()
        {
            [typeof(string)] = "string",
            [typeof(char)] = "string",
            [typeof(Guid)] = "string",
            [typeof(DateTimeOffset)] = "string",
            [typeof(byte)] = "number",
            [typeof(sbyte)] = "number",
            [typeof(short)] = "number",
            [typeof(ushort)] = "number",
            [typeof(int)] = "number",
            [typeof(uint)] = "number",
            [typeof(long)] = "number",
            [typeof(ulong)] = "number",
            [typeof(float)] = "number",
            [typeof(double)] = "number",
            [typeof(decimal)] = "number",
            [typeof(bool)] = "boolean",
            [typeof(object)] = "any",
            [typeof(void)] = "void",
        };


        public static void GenerateTypeScriptInterfaces(this IApplicationBuilder app, Assembly dtoAssembly, string outputPath, 
            string inputNamespace, string masterDtoFileName = "dto.exports.ts")
        {
            _inputNamespace = inputNamespace;

            Type[] typesToConvert = GetTypesToConvert(dtoAssembly);

            var dtoNames = new List<string>();

            foreach (Type type in typesToConvert)
            {
                var tsType = ConvertToTypeScript(type);
                
                dtoNames.Add(tsType.Name);
                
                string fullPath = Path.Combine(outputPath, tsType.Name);

                string directory = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllLines(fullPath, tsType.Lines);
            }

            if (typesToConvert.Length == 0) return;
            
            string dtoImportsPath = Path.Combine(outputPath, masterDtoFileName);
            
            // Take the dto filenames from above, strip off the extension and format into the string template
            File.WriteAllLines(dtoImportsPath, dtoNames
                .Select(dtoName => dtoName.Remove(dtoName.Length - 3))
                .Select(nameWithoutExtenstion => 
                    string.Format(ImportExportTemplate, "export", nameWithoutExtenstion)).ToArray());
        }
        private static Type[] GetTypesToConvert(Assembly assembly)
        {
            var types = assembly.GetTypes().Where(x =>
                x.IsClass &&
                !x.IsAbstract &&
                x.Namespace == _inputNamespace);

            return types
                .Select(ReplaceByGenericArgument)
                .Where(t => !t.IsPrimitive && !nonPrimitivesExcludeList.Contains(t))
                .Distinct()
                .ToArray();
        }
        
        private static Type ReplaceByGenericArgument(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }

            if (!type.IsConstructedGenericType)
            {
                return type;
            }

            var genericArgument = type.GenericTypeArguments.First();

            var isTask = type.GetGenericTypeDefinition() == typeof(Task<>);
            var isEnumerable = typeof(IEnumerable<>).MakeGenericType(genericArgument).IsAssignableFrom(type);

            if (!isTask && !isEnumerable)
            {
                throw new InvalidOperationException();
            }

            if (genericArgument.IsConstructedGenericType)
            {
                return ReplaceByGenericArgument(genericArgument);
            }

            return genericArgument;
        }

        private static (string Name, string[] Lines) ConvertToTypeScript(Type type)
        {
            string filename = $"{type.Name}.ts";

            Type[] types = GetAllNestedTypes(type);

            var lines = new List<string>();

            foreach (Type t in types)
            {
                if (t.IsClass || t.IsInterface)
                {
                    ConvertClassOrInterface(lines, t);
                }
                else if (t.IsEnum) {
                    ConvertEnum(lines, t);
                }
            }

            return (filename, lines.ToArray());
        }
        
        private static void ConvertClassOrInterface(IList<string> lines, Type type)
        {
            // get list of public properties to import/export
            var properties = type.GetProperties().Where(p => p.GetMethod.IsPublic);

            var importTypeNames = new List<string>();
            
            // Get properties that are other DTOs and add import statements
            foreach (var property in properties)
            {
                var propType = property.PropertyType;
                
                // Skip if its a parent of itself 
                if(propType.Name == type.Name) continue;
                
                // Skip all the base types
                if(convertedTypes.ContainsKey(propType)) continue;

                var typeName = propType.Name;
                
                if (propType.IsConstructedGenericType)
                {
                    if(propType.GetGenericTypeDefinition() == typeof(ICollection<>))
                    {
                        // Extract the type from a one to many relationship
                        var collectionType = propType.GenericTypeArguments[0];
                        typeName = collectionType.Name;
                    }
                    else continue; // skip any nullables
                }

                // Skip if the type has already been imported
                if(importTypeNames.Contains(typeName)) continue;
                    
                lines.Add(string.Format(ImportExportTemplate, "import", typeName));
                    
                importTypeNames.Add(typeName);
            }
            
            lines.Add("");
            
            lines.Add($"export interface {type.Name} {{");

            foreach (PropertyInfo property in type.GetProperties().Where(p => p.GetMethod.IsPublic))
            {
                Type propType = property.PropertyType;
                Type arrayType = GetArrayOrEnumerableType(propType);
                Type nullableType = GetNullableType(propType);

                Type typeToUse = nullableType ?? arrayType ?? propType;


                var convertedType = ConvertType(typeToUse);

                string suffix = "";
                suffix = arrayType != null ? "[]" : suffix;
                suffix = nullableType != null ? "|null" : suffix;

                lines.Add($"  {CamelCaseName(property.Name)}: {convertedType}{suffix};");
            }

            lines.Add($"}}");
        }

        private static string ConvertType(Type typeToUse)
        {
            if (convertedTypes.ContainsKey(typeToUse))
            {
                return convertedTypes[typeToUse];
            }

            if (typeToUse.IsConstructedGenericType && typeToUse.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                var keyType = typeToUse.GenericTypeArguments[0];
                var valueType = typeToUse.GenericTypeArguments[1];
                return $"{{ [key: {ConvertType(keyType)}]: {ConvertType(valueType)} }}";
            }

            return typeToUse.Name;
        }

        private static void ConvertEnum(IList<string> lines, Type type)
        {
            var enumValues = type.GetEnumValues().Cast<int>().ToArray();
            var enumNames = type.GetEnumNames();

            lines.Add($"export enum {type.Name} {{");

            for (int i = 0; i < enumValues.Length; i++)
            {
                lines.Add($"  {enumNames[i]} = {enumValues[i]},");
            }

            lines.Add($"}}");
        }

        private static Type[] GetAllNestedTypes(Type type)
        {
            return new Type[] { type }
                .Concat(type.GetNestedTypes().SelectMany(nt => GetAllNestedTypes(nt)))
                .ToArray();
        }

        private static Type GetArrayOrEnumerableType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }

            else if (type.IsConstructedGenericType)
            {
                Type typeArgument = type.GenericTypeArguments.First();

                if (typeof(IEnumerable<>).MakeGenericType(typeArgument).IsAssignableFrom(type))
                {
                    return typeArgument;
                }
            }

            return null;
        }

        private static Type GetNullableType(Type type)
        {
            if (type.IsConstructedGenericType)
            {
                Type typeArgument = type.GenericTypeArguments.First();

                if (typeArgument.IsValueType && typeof(Nullable<>).MakeGenericType(typeArgument).IsAssignableFrom(type))
                {
                    return typeArgument;
                }
            }

            return null;
        }

        private static string CamelCaseName(string pascalCaseName)
        {
            return pascalCaseName[0].ToString().ToLower() + pascalCaseName.Substring(1);
        }
    }
}