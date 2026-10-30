using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MC2Pack;

namespace Astera
{
    static class MultiverseTrcAudit
    {
        static void Require(bool ok, string message)
        {
            if (!ok)
                throw new IOException(message);
        }

        internal static IEnumerable<string> Run()
        {
            var checks = new List<string>();
            Require(new Preferences().FtlRenegadeCollection && Engine.Json.Deserialize<Preferences>("{}").FtlRenegadeCollection, "TRC must default on for new and legacy preferences");
            Require(!Engine.Json.Deserialize<Preferences>(Engine.Json.Serialize(new Preferences { FtlRenegadeCollection = false })).FtlRenegadeCollection, "Saved TRC-off selection was lost");
            checks.Add("TRC defaults on for new/legacy settings; explicitly disabled selection survives serialization");
            var plan = new FtlPlan
            {
                DataVersion = "5.5.1",
                AssetsVersion = "5.5",
                HyperspaceVersion = "1.23.2",
                Stage = "stage",
                Assets = new FtlAsset
                {
                    Name = "assets"
                },
                Data = new FtlAsset
                {
                    Name = "data"
                },
                Tool = new FtlAsset
                {
                    Name = "tool"
                }
            };
            Multiverse.SelectGibs(plan, true);
            Multiverse.SelectTrc(plan, true);
            foreach (var asset in Multiverse.Payloads(plan))
                plan.Downloads[asset.Name] = asset.Name;
            Require(Multiverse.Payloads(plan).Count() == 6 && Multiverse.PatchArguments(plan).Skip(3).SequenceEqual(new[] { "assets", "data", plan.Trc.Name, plan.Gibs.Name, plan.TrcGibs.Name }), "Wrong TRC/debris download set or patch order");
            var installed = new FtlState
            {
                DataVersion = "5.5.1",
                AssetsVersion = "5.5",
                HyperspaceVersion = "1.23.2",
                GibsVersion = "1.4.2"
            };
            Require(Multiverse.NeedsUpdate(installed, plan), "Existing base install did not offer TRC");
            installed.TrcVersion = plan.TrcVersion;
            installed.TrcGibsVersion = plan.TrcGibsVersion;
            Require(!Multiverse.NeedsUpdate(installed, plan), "Identical TRC stack repeatedly offered an update");
            checks.Add("TRC follows Data; base and TRC debris follow their dependencies and participate in exact update detection");
            Multiverse.SelectTrc(plan, false);
            Require(plan.Trc == null && plan.TrcGibs == null && plan.Gibs != null && Multiverse.NeedsUpdate(installed, plan), "Disabling TRC did not retain base debris and request a clean rebuild");
            Multiverse.SelectGibs(plan, false);
            Multiverse.SelectTrc(plan, true);
            Require(plan.Trc != null && plan.TrcGibs == null && Multiverse.Payloads(plan).Count() == 4, "TRC without optional debris selected an invalid dependency");
            checks.Add("TRC and debris can be switched off independently without orphan TRC debris");
            plan.DataVersion = "5.6";
            bool refused = false;
            try
            {
                Multiverse.SelectTrc(plan, true);
            }
            catch (IOException)
            {
                refused = true;
            }

            Require(refused, "Future unverified TRC compatibility accepted");
            checks.Add("Pinned TRC2.6 refuses unverified future Multiverse versions");
            return checks;
        }
    }
}
