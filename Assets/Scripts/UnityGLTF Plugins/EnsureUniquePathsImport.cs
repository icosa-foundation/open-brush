using System.Text.RegularExpressions;
using GLTF.Schema;

namespace UnityGLTF.Plugins
{

    public class EnsureUniquePathsImport : GLTFImportPlugin
    {
        public override string DisplayName => "Ensure Unique Paths Import Plugin";
        public override string Description => "Adds a suffix for all nodes to ensure their path from root is unique.";
        public override GLTFImportPluginContext CreateInstance(GLTFImportContext context)
        {
            return new EnsureUniquePathsImportContext();
        }
    }

    public class EnsureUniquePathsImportContext : GLTFImportPluginContext
    {
        // CRITICAL: This logic must match GenerateUniqueNames() in Model.cs exactly.
        // Both functions ensure unique node names using the same naming pattern and safety checks.
        // If you modify this function, you MUST update GenerateUniqueNames() accordingly.
        public override void OnAfterImportRoot(GLTFRoot gltfRoot)
        {
            if (gltfRoot.Nodes == null) return;

            // Find root nodes (nodes not referenced as children by other nodes)
            var childNodeIds = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < gltfRoot.Nodes.Count; i++)
            {
                var node = gltfRoot.Nodes[i];
                if (node?.Children != null)
                {
                    foreach (NodeId childId in node.Children)
                    {
                        childNodeIds.Add(childId.Id);
                    }
                }
            }

            // Rename root nodes first, then process each root node's hierarchy
            int rootIndex = 0;
            for (int i = 0; i < gltfRoot.Nodes.Count; i++)
            {
                if (!childNodeIds.Contains(i))
                {
                    var rootNode = gltfRoot.Nodes[i];
                    if (rootNode != null)
                    {
                        string oldName = rootNode.Name ?? "";
                        // Skip renaming if already has our suffix (safety check - matches Model.GenerateUniqueNames)
                        if (!Regex.IsMatch(oldName, @"\[ob:\d+\]$"))
                        {
                            rootNode.Name = oldName + $"[ob:{rootIndex}]";
                        }
                    }
                    ProcessNodeHierarchy(gltfRoot, i);
                    rootIndex++;
                }
            }
        }

        private void ProcessNodeHierarchy(GLTFRoot gltfRoot, int nodeId)
        {
            var node = gltfRoot.Nodes[nodeId];
            if (node?.Children != null)
            {
                int childIndex = 0;
                foreach (var childId in node.Children)
                {
                    var childNode = gltfRoot.Nodes[childId.Id];
                    if (childNode != null)
                    {
                        string oldName = childNode.Name ?? "";
                        // Skip renaming if already has our suffix
                        if (!Regex.IsMatch(oldName, @"\[ob:\d+\]$"))
                        {
                            childNode.Name = oldName + $"[ob:{childIndex}]";
                        }
                    }
                    childIndex++;
                    
                    // Recursively process this child's children (matches Model.GenerateUniqueNames)
                    ProcessNodeHierarchy(gltfRoot, childId.Id);
                }
            }
        }
    }
}
