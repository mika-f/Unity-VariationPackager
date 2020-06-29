﻿using System.Collections.Generic;

namespace Mochizuki.VariationPackager.Models.Interface
{
    public interface IPackageConfiguration
    {
        string Name { get; }

        string BaseDir { get; }

        List<string> Includes { get; }

        List<string> Excludes { get; }
    }
}