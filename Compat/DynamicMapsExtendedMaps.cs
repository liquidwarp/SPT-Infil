using System;
using System.Collections.Generic;
using DynamicMaps.Data;
using Liquidwarp.Infil.Map;

namespace Liquidwarp.Infil.Compat;

public static class DynamicMapsExtendedMaps
{
    private static readonly Dictionary<string, string> BackportMapNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["interchange"] = "dmext_interchange_backport",
        ["factory4_day"] = "dmext_factory_classic",
        ["factory4_night"] = "dmext_factory_classic",
        ["laboratory"] = "dmext_laboratory_backport",
    };

    public static MapDef FindBackport(string locationId) =>
        locationId != null && BackportMapNames.TryGetValue(locationId, out var name) ? MapDefCatalog.Find(name) : null;
}
