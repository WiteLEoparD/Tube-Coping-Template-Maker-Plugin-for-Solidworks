using System;
using System.Collections.Generic;
using BajaSAE.TubeCutTemplateAddin.Core.Models;

namespace BajaSAE.TubeCutTemplateAddin.Core.BSAE
{
    public class BsaeNode
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Role { get; set; }
        public Point3D CoordinateMm { get; set; }
        public bool IsPrimary { get; set; } = true;
    }

    public class BsaeMemberSpec
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string StartNode { get; set; }
        public string EndNode { get; set; }
        public string Category { get; set; }
        public string RuleRef { get; set; }
        public double MinOdInches { get; set; }
        public double MinWallInches { get; set; }
    }

    /// <summary>
    /// Implements Section B.3 (Roll Cage) of the 2027 Baja SAE Ruleset (Rev A),
    /// specifically the Rear Braced Frame (RBF) node policy and centerline offset validation.
    /// </summary>
    public class BsaeRbfPolicyManager
    {
        public const double PrimaryMinOdInches = 1.250;
        public const double PrimaryMinWallInches = 0.065;
        public const double SecondaryMinOdInches = 1.000;
        public const double SecondaryMinWallInches = 0.035;

        // BSAE Node Policy: Maximum allowable centerline offset at any node is 2.000" (50.8 mm)
        public const double MaxNodeOffsetInches = 2.000;
        public const double MaxNodeOffsetMm = 50.800;

        private readonly Dictionary<string, BsaeNode> _registeredNodes = new Dictionary<string, BsaeNode>(StringComparer.OrdinalIgnoreCase);
        private readonly List<BsaeMemberSpec> _standardMembers = new List<BsaeMemberSpec>();

        public BsaeRbfPolicyManager()
        {
            InitializeStandardRbfNodes();
            InitializeStandardRbfMembers();
        }

        private void InitializeStandardRbfNodes()
        {
            // Standard Named Points in BSAE 2027 Rev A Rear Braced Frame (RBF)
            // Typical reference coordinates in mm (customized per CAD model if nodes exist)
            AddNode("B_L", "Rear Roll Hoop Left Upper Corner (RRH / RHO / FAB_Upper)", "Primary Roll Cage Node (Art. B.3.2)", new Point3D(-300, 0, 950));
            AddNode("B_R", "Rear Roll Hoop Right Upper Corner (RRH / RHO / FAB_Upper)", "Primary Roll Cage Node (Art. B.3.2)", new Point3D(300, 0, 950));

            AddNode("S_L", "Rear Roll Hoop Left Intermediate Node (RRH / SIM / FAB_Lower)", "Primary Roll Cage Node (Art. B.3.2)", new Point3D(-350, 0, 500));
            AddNode("S_R", "Rear Roll Hoop Right Intermediate Node (RRH / SIM / FAB_Lower)", "Primary Roll Cage Node (Art. B.3.2)", new Point3D(350, 0, 500));

            AddNode("A_L", "Rear Roll Hoop Left Bottom Node (RRH / LFS / LC)", "Primary Roll Cage Node (Art. B.3.2)", new Point3D(-350, 0, 50));
            AddNode("A_R", "Rear Roll Hoop Right Bottom Node (RRH / LFS / LC)", "Primary Roll Cage Node (Art. B.3.2)", new Point3D(350, 0, 50));

            AddNode("R_L", "Rear Braced Frame Left Aft Vertex (FAB_Upper_L / FAB_Lower_L / RLC)", "Rear Bracing Primary Node (Art. B.3.2.13)", new Point3D(-250, -550, 500));
            AddNode("R_R", "Rear Braced Frame Right Aft Vertex (FAB_Upper_R / FAB_Lower_R / RLC)", "Rear Bracing Primary Node (Art. B.3.2.13)", new Point3D(250, -550, 500));

            AddNode("C_L", "Roll Hoop Overhead Left Forward Node (RHO / FBM)", "Primary Roll Cage Node (Art. B.3.2)", new Point3D(-280, 600, 950));
            AddNode("C_R", "Roll Hoop Overhead Right Forward Node (RHO / FBM)", "Primary Roll Cage Node (Art. B.3.2)", new Point3D(280, 600, 950));

            AddNode("D_L", "Front Roll Hoop Left Lower Node (FBM / SIM / LFS)", "Primary Roll Cage Node (Art. B.3.2)", new Point3D(-320, 1100, 200));
            AddNode("D_R", "Front Roll Hoop Right Lower Node (FBM / SIM / LFS)", "Primary Roll Cage Node (Art. B.3.2)", new Point3D(320, 1100, 200));
        }

        private void InitializeStandardRbfMembers()
        {
            _standardMembers.Add(new BsaeMemberSpec
            {
                Key = "FAB_Upper_L",
                DisplayName = "Rear Upper Brace - Left (FAB-U-L)",
                StartNode = "B_L",
                EndNode = "R_L",
                Category = "Primary Member (Rear Bracing)",
                RuleRef = "BSAE 2027 Rev A - Art. B.3.2.13",
                MinOdInches = 1.250,
                MinWallInches = 0.065
            });

            _standardMembers.Add(new BsaeMemberSpec
            {
                Key = "FAB_Upper_R",
                DisplayName = "Rear Upper Brace - Right (FAB-U-R)",
                StartNode = "B_R",
                EndNode = "R_R",
                Category = "Primary Member (Rear Bracing)",
                RuleRef = "BSAE 2027 Rev A - Art. B.3.2.13",
                MinOdInches = 1.250,
                MinWallInches = 0.065
            });

            _standardMembers.Add(new BsaeMemberSpec
            {
                Key = "FAB_Lower_L",
                DisplayName = "Rear Lower Brace - Left (FAB-L-L)",
                StartNode = "S_L",
                EndNode = "R_L",
                Category = "Primary Member (Rear Bracing)",
                RuleRef = "BSAE 2027 Rev A - Art. B.3.2.13",
                MinOdInches = 1.250,
                MinWallInches = 0.065
            });

            _standardMembers.Add(new BsaeMemberSpec
            {
                Key = "FAB_Lower_R",
                DisplayName = "Rear Lower Brace - Right (FAB-L-R)",
                StartNode = "S_R",
                EndNode = "R_R",
                Category = "Primary Member (Rear Bracing)",
                RuleRef = "BSAE 2027 Rev A - Art. B.3.2.13",
                MinOdInches = 1.250,
                MinWallInches = 0.065
            });

            _standardMembers.Add(new BsaeMemberSpec
            {
                Key = "RLC",
                DisplayName = "Rear Lateral Crossmember (RLC)",
                StartNode = "R_L",
                EndNode = "R_R",
                Category = "Primary Member (Lateral Crossmember)",
                RuleRef = "BSAE 2027 Rev A - Art. B.3.2.13.2",
                MinOdInches = 1.000,
                MinWallInches = 0.065
            });

            _standardMembers.Add(new BsaeMemberSpec
            {
                Key = "RRH_Upper",
                DisplayName = "Rear Roll Hoop Upper Crossmember",
                StartNode = "B_L",
                EndNode = "B_R",
                Category = "Primary Member (Roll Hoop)",
                RuleRef = "BSAE 2027 Rev A - Art. B.3.2.1",
                MinOdInches = 1.250,
                MinWallInches = 0.065
            });

            _standardMembers.Add(new BsaeMemberSpec
            {
                Key = "RRH_Diag",
                DisplayName = "Rear Roll Hoop Diagonal Bracing",
                StartNode = "B_L",
                EndNode = "S_R",
                Category = "Primary Member (Triangulation)",
                RuleRef = "BSAE 2027 Rev A - Art. B.3.2.4",
                MinOdInches = 1.000,
                MinWallInches = 0.065
            });

            _standardMembers.Add(new BsaeMemberSpec
            {
                Key = "LC_Intermediate",
                DisplayName = "Lateral Crossmember at S (LC-S)",
                StartNode = "S_L",
                EndNode = "S_R",
                Category = "Primary Member (Lateral Crossmember)",
                RuleRef = "BSAE 2027 Rev A - Art. B.3.2.5",
                MinOdInches = 1.000,
                MinWallInches = 0.065
            });

            _standardMembers.Add(new BsaeMemberSpec
            {
                Key = "LC_Lower",
                DisplayName = "Lower Lateral Crossmember (LC-A)",
                StartNode = "A_L",
                EndNode = "A_R",
                Category = "Primary Member (Lateral Crossmember)",
                RuleRef = "BSAE 2027 Rev A - Art. B.3.2.5",
                MinOdInches = 1.000,
                MinWallInches = 0.065
            });
        }

        private void AddNode(string name, string description, string role, Point3D coord)
        {
            _registeredNodes[name] = new BsaeNode
            {
                Name = name,
                Description = description,
                Role = role,
                CoordinateMm = coord
            };
        }

        public void RegisterNodeCoordinate(string name, Point3D coord)
        {
            if (_registeredNodes.ContainsKey(name))
            {
                _registeredNodes[name].CoordinateMm = coord;
            }
            else
            {
                AddNode(name, $"User Node {name}", "Custom Node", coord);
            }
        }

        /// <summary>
        /// Finds the closest BSAE named node to a given 3D tube cut endpoint.
        /// Returns node name and offset distance in mm.
        /// </summary>
        public (string NodeName, double DistanceMm, bool Compliant) IdentifyClosestNode(Point3D endpoint)
        {
            string closestNode = "UNMAPPED";
            double minDistance = double.MaxValue;

            foreach (var kvp in _registeredNodes)
            {
                double dist = kvp.Value.CoordinateMm.DistanceTo(endpoint);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestNode = kvp.Key;
                }
            }

            bool compliant = minDistance <= MaxNodeOffsetMm;
            return (closestNode, minDistance, compliant);
        }

        /// <summary>
        /// Classifies a tube member by its 3D endpoints or CAD name hint.
        /// </summary>
        public (string MemberName, string Category, string RuleRef, string StartNode, string EndNode) ClassifyMember(
            Point3D pt1, Point3D pt2, string nameHint = "")
        {
            var match1 = IdentifyClosestNode(pt1);
            var match2 = IdentifyClosestNode(pt2);

            string n1 = match1.NodeName;
            string n2 = match2.NodeName;

            // Search for direct node match
            foreach (var mem in _standardMembers)
            {
                if ((string.Equals(n1, mem.StartNode, StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(n2, mem.EndNode, StringComparison.OrdinalIgnoreCase)) ||
                    (string.Equals(n1, mem.EndNode, StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(n2, mem.StartNode, StringComparison.OrdinalIgnoreCase)))
                {
                    return (mem.DisplayName, mem.Category, mem.RuleRef, n1, n2);
                }
            }

            // Fallback by CAD name hint (e.g., "FAB_Upper", "RLC", "RRH")
            if (!string.IsNullOrEmpty(nameHint))
            {
                foreach (var mem in _standardMembers)
                {
                    if (nameHint.IndexOf(mem.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return (mem.DisplayName, mem.Category, mem.RuleRef, mem.StartNode, mem.EndNode);
                    }
                }
            }

            // Generic chassis tube member
            return ($"Chassis Member ({n1} - {n2})", "Chassis Member", "BSAE 2027 Rev A - Art. B.3", n1, n2);
        }
    }
}
