using System.Text;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services.OpeningBalances;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class OpeningBalanceCsvTests
{
    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void ParsesCommaFile()
    {
        var (rows, err) = OpeningBalanceCsv.Parse(Bytes("sku,location,qty,value_eur\nA-1,R1-A,10,25.50\nB-2,R1-B,2.5,7\n"));
        err.Should().BeNull();
        rows.Should().HaveCount(2);
        rows[0].Qty.Should().Be(10m);
        rows[0].ValueEur.Should().Be(25.50m);
        rows.Sum(r => r.ValueEur!.Value).Should().Be(32.50m);
    }

    [Fact]
    public void SemicolonFileAcceptsDecimalComma()
    {
        var (rows, _) = OpeningBalanceCsv.Parse(Bytes("﻿SKU;Location;Qty;Value_EUR\r\nA-1;R1-A;10,5;25,50\r\n"));
        rows.Single().Qty.Should().Be(10.5m);
        rows.Single().ValueEur.Should().Be(25.50m);
    }

    [Fact]
    public void FlagsNegativeAndInvalidValues()
    {
        var (rows, _) = OpeningBalanceCsv.Parse(Bytes("sku,location,qty,value_eur\nA-1,R1,-1,5\nA-2,R1,1,-5\nA-3,R1,x,5\n"));
        rows[0].Errors.Should().Contain(OpeningBalanceCsv.ErrNegativeQty);
        rows[1].Errors.Should().Contain(OpeningBalanceCsv.ErrNegativeValue);
        rows[2].Errors.Should().Contain(OpeningBalanceCsv.ErrInvalidNumber);
    }

    [Fact]
    public void ReportsFileLevelErrorsAndStableHash()
    {
        OpeningBalanceCsv.Parse(Bytes("")).FileError.Should().Be("EmptyFile");
        OpeningBalanceCsv.Parse(Bytes("sku,qty\nA,1\n")).FileError.Should().Be("MissingColumns");
        OpeningBalanceCsv.Hash(Bytes("abc")).Should().Be(OpeningBalanceCsv.Hash(Bytes("abc")));
        OpeningBalanceCsv.Hash(Bytes("abc")).Should().NotBe(OpeningBalanceCsv.Hash(Bytes("abd")));
    }
}
