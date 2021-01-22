using System;

namespace ExampleProject.Dtos
{
    public class TransactionDto
    {
        public int Id { get; set; }
        public string Transactor { get; set; }
        public DateTimeOffset Date { get; set; }
        public double Amount { get; set; }
        public AccountDto Account { get; set; }
    }
}