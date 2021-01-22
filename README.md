# Api To Client Helper

 This library can be used in an Asp.Net Core 3.1 web api project to convert DTOs to typescript interfaces

## Getting Started

1. Copy and Reference the .dll in the DLL folder from the solution directory into your project

2. In the Configure method of your Startup class add this extension method: `app.GenerateTypeScriptInterfaces(Assembly.GetExecutingAssembly(), YourOutputPathHere, NamespaceOfYourDtos)`

3. Checkout the ExampleProject Startup.cs for reference.

4. Once you run your api it will look for class objects in the provided Dto Namespace and convert them to typescript interfaces.  An extra file will be made called dto.exports.ts.  You can add your desired name for this file as a parameter in the extension method like this: `app.GenerateTypeScriptInterfaces(Assembly.GetExecutingAssembly(), YourOutputPathHere, NamespaceOfYourDtos, NameOfYourDtoExportFile)`

## Example Code

Startup.cs
```c#
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            string outputFolder = Configuration.GetSection("TsOutputFolder").Value;
            string outputPath = Path.Combine(env.ContentRootPath, outputFolder);
            string inputNamespace = Configuration.GetSection("DtoNamespace").Value;
            
            app.GenerateTypeScriptInterfaces(Assembly.GetExecutingAssembly(), outputPath, inputNamespace);
            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();
            ...
```

appsettings.json
```json
    ...
    "TsOutputFolder": "./OutputTypescript",
    "DtoNamespace": "ExampleProject.Dtos"
    }
```

based on code by lmcarreiro @ https://github.com/lmcarreiro/cs2ts-example