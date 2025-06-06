using System.Linq;

namespace Libs.TLV
{
    /// <summary>
    /// Represents a node in a parsed TLV (Tag-Length-Value) structure.
    /// Provides access to nested TLV elements via an indexer and exposes
    /// the value of a primitive TLV element as a hexadecimal string.
    /// </summary>
    public class TlvObject
    {
        /// <summary>
        /// Underlying dictionary storing child nodes or primitive values.
        /// Keys are tag names (or raw tag hex), and values are either:
        /// • string: a hex‐encoded primitive value, or
        /// • Dictionary&lt;string, object&gt;: a nested TLV subtree.
        /// </summary>
        private Dictionary<string, object> sourceNode = new Dictionary<string, object>();

        public Dictionary<string, object> Source { get => sourceNode; }


        /// <summary>
        /// Gets the primitive value of this TLV object as a hexadecimal string.
        /// If the instance represents a constructed node (i.e., has children),
        /// this property will be <c>null</c>.
        /// </summary>
        public string Value { get; } = null;

        public bool IsEmpty { get => sourceNode.Count() == 0 && Value == null; }

        /// <summary>
        /// Indexer to access a child TLV element by its tag name.
        /// </summary>
        /// <param name="index">
        /// The tag name (or raw tag hex) of the desired child element.
        /// </param>
        /// <returns>
        /// A <see cref="TlvObject"/> representing the nested TLV element:
        /// • If the specified tag does not exist, an empty <see cref="TlvObject"/> is returned.
        /// • If the specified tag is a primitive element, the returned object's <see cref="Value"/>
        ///   property contains that element’s hex string.
        /// • If the specified tag is constructed (has children), the returned object wraps
        ///   the nested dictionary of child elements.
        /// </returns>
        public TlvObject this[string index]
        {
            get
            {
                // supporting addressing tag by tag hex value (for this which have name)
                if (TLV.TlvParser.TagNames.ContainsKey(index.ToUpper()))
                    index = $"{TlvParser.TagNames[index.ToUpper()]}({index.ToUpper()})" ;
                if (!sourceNode.ContainsKey(index))
                    // Return an “empty” TlvObject (no value, no children)
                    return new TlvObject();
                if (sourceNode[index].GetType() == typeof(string))
                    // Wrap the primitive value
                    return new TlvObject(sourceNode[index]);
                // Wrap the nested dictionary
                return new TlvObject(((Dictionary<string, object>)sourceNode[index]));
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TlvObject"/> class
        /// representing an empty TLV node (no children, no value).
        /// </summary>
        private TlvObject()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TlvObject"/> class
        /// that wraps a primitive TLV value.
        /// </summary>
        /// <param name="value">
        /// The primitive TLV value as a hexadecimal string.
        /// </param>
        private TlvObject(object value)
        {
            Value = (string)value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TlvObject"/> class
        /// that wraps a constructed TLV node (with child elements).
        /// </summary>
        /// <param name="source">
        /// A <see cref="Dictionary{String,Object}"/> containing the nested TLV structure.
        /// Each key is a tag name (or raw tag hex), and each value is either a string
        /// (primitive hex value) or another dictionary for further nesting.
        /// </param>
        public TlvObject(Dictionary<string, object> source)
        {
            sourceNode = source;
        }
    }

}