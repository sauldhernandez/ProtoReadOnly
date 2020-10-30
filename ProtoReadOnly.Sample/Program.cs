using System;
using Org.Sample;

IReadOnlyMyEvent e = new MyEvent
{
    Id = "theId",
    Inner = new Inner
    {
        Maps = { ["first"] = new OneOfs { First = "first" }, ["second"] = new OneOfs { Second = 2 } }
    }
};

Console.WriteLine(e.Id);
Console.WriteLine(e.Inner.Maps["first"].First);