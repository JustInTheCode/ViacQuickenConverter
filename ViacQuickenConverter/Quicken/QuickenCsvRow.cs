using System;
using CsvHelper.Configuration.Attributes;

namespace ViacQuickenConverter.Quicken
{
    public class QuickenCsvRow
    {
        [Name("action")]
        public required string Action { get; set; }

        [Name("date")]
        public required string Date { get; set; }

        [Name("account")]
        public required string Account { get; set; }

        [Name("security")]
        public string? Security { get; set; }

        [Name("optionalSymbol")]
        public string? OptionalSymbol { get; set; }

        [Name("shares")]
        public decimal? Shares { get; set; }

        [Name("price")]
        public decimal? Price { get; set; }

        [Name("amount")]
        public required decimal Amount { get; set; }

        [Name("commissionFee")]
        public decimal? CommissionFee { get; set; }

        [Name("num")]
        public string? Num { get; set; }

        [Name("payee")]
        public string? Payee { get; set; }

        [Name("memo")]
        public string? Memo { get; set; }

        [Name("clearedStatus")]
        public string? ClearedStatus { get; set; }

        [Name("category")]
        public string? Category { get; set; }

        [Name("tag")]
        public string? Tag { get; set; }

        [Name("transferAccount")]
        public string? TransferAccount { get; set; }

        [Name("basisDate")]
        public DateTime? BasisDate { get; set; }
    }
}