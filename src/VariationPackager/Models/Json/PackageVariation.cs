﻿/*-------------------------------------------------------------------------------------------
 * Copyright (c) Fuyuno Mikazuki / Natsuneko. All rights reserved.
 * Licensed under the MIT License. See LICENSE in the project root for license information.
 *------------------------------------------------------------------------------------------*/

using Mochizuki.VariationPackager.Models.Interface;

using Newtonsoft.Json;

namespace Mochizuki.VariationPackager.Models.Json
{
    public class PackageVariation : IPackageVariation
    {
        [JsonConstructor]
        public PackageVariation([JsonProperty("archive")] PackageConfiguration archive, [JsonProperty("unitypackage")] PackageConfiguration unityPackage)
        {
            Archive = archive;
            UnityPackage = unityPackage;
        }

        public IPackageConfiguration Archive { get; }

        public IPackageConfiguration UnityPackage { get; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}