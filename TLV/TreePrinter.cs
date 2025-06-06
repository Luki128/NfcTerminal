using System;
using System.Collections.Generic;
using System.Text;

namespace Libs.TLV
{
    /// <summary>
    /// Provides functionality to generate an ASCII-art representation of a tree
    /// stored in a nested Dictionary&lt;string, object&gt; structure.
    /// </summary>
    public static class TreePrinter
    {
        /// <summary>
        /// Generates a string containing an ASCII-art diagram of the tree.
        /// </summary>
        /// <param name="tree">
        /// A Dictionary&lt;string, object&gt; where each key is a node label.
        /// If the associated value is a string, it is treated as a leaf (“key: value”).
        /// If the value is another Dictionary&lt;string, object&gt;, it represents an internal node
        /// whose children will be rendered recursively.
        /// </param>
        /// <returns>
        /// A multi-line string, using connectors (“├──”, “└──”) and indent-prefixes
        /// (“│   ” or spaces) to visualize the hierarchy.
        /// </returns>
        public static string GenerateTreeString(Dictionary<string, object> tree)
        {
            var sb = new StringBuilder();
            // At the root level, we call PrintNode with an empty indent. 
            // The 'true' flag here only affects how childIndent is computed, 
            // but since indent is empty, it will not actually prepend any connector at the topmost call.
            PrintNode(sb, tree, indent: string.Empty, isLast: true);
            return sb.ToString();
        }

        /// <summary>
        /// Recursively appends lines to <paramref name="sb"/> to render each node and leaf.
        /// </summary>
        /// <param name="sb">
        /// The StringBuilder to which ASCII-art lines will be appended.
        /// </param>
        /// <param name="currentDict">
        /// The dictionary representing the current subtree. Each entry’s key is a node label.
        /// If its value is:
        ///   • string  : Render as a leaf “key: value”.
        ///   • Dictionary&lt;string, object&gt; : Render as an internal node and recurse.
        ///   • any other object: Render as “key: <object.ToString()>”.
        /// </param>
        /// <param name="indent">
        /// A prefix string that builds up “│   ” or spaces (“    ”) from parent levels.
        /// This ensures vertical lines (“│”) appear where branches continue.
        /// </param>
        /// <param name="isLast">
        /// Indicates whether the current dictionary is the last sibling at its parent level.
        /// If true, use “└── ” connector; if false, use “├── ” connector.
        /// </param>
        private static void PrintNode(
            StringBuilder sb,
            Dictionary<string, object> currentDict,
            string indent,
            bool isLast)
        {
            // Extract all keys to a list so we can detect “last sibling” by index.
            var keys = new List<string>(currentDict.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                bool lastSibling = i == keys.Count - 1;

                // Choose the appropriate connector:
                // • “└──” if this is the last sibling in its group.
                // • “├──” otherwise.
                string connector = lastSibling ? "└── " : "├── ";

                object value = currentDict[key];

                if (value is string leafValue)
                {
                    // Leaf node: render “key: value”
                    sb.Append(indent);
                    sb.Append(connector);
                    sb.AppendLine($"{key}: {leafValue}");
                }
                else if (value is Dictionary<string, object> childDict)
                {
                    // Internal node: render just the key, then recurse into children.
                    sb.Append(indent);
                    sb.Append(connector);
                    sb.AppendLine(key);

                    // Compute the indent for child lines:
                    // • If this node is the last sibling, subsequent lines use only spaces.
                    //   That means the branch stops here (no vertical “│”).
                    // • Otherwise, we include a vertical bar “│   ” to indicate continuation.
                    string childIndent = indent + (lastSibling ? "    " : "│   ");

                    // Recurse with childIndent. We pass 'true' for isLast in the recursive call,
                    // but it’s not actually used in forming connectors at this call level—
                    // instead, connector logic is handled at the start of the next loop.
                    PrintNode(sb, childDict, childIndent, isLast: true);
                }
                else
                {
                    // Unexpected type: treat as ToString() leaf.
                    sb.Append(indent);
                    sb.Append(connector);
                    sb.AppendLine($"{key}: {value}");
                }
            }
        }
    }
}
