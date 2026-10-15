using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace FleetTrack.Domain;

internal sealed class Class1
{
    public static void Test()
    {
        var entity = new DbContext(new DbContextOptionsBuilder().Options);
    }
}