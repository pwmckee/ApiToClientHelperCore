using System.Collections.Generic;

namespace ExampleProject.Dtos
{
    public class AccountDto
    {
        public int Id { get; set; }
        public string Name2 { get; set; }
        public ICollection<TransactionDto> Transactions { get; set; }
    }
}