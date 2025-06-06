using System.Text.Json;
namespace Libs.TLV
{
    public static class TlvParser
    {
        /// <summary>
        /// Maps hex tag key to it name
        /// </summary>
        public static Dictionary<string, string> TagNames = new Dictionary<string, string>()
    {
        { "6F","FCI Template" },
        { "A5","FCI Template" },
        { "84","DF Name" },
        { "4F","ADF Name" },
        { "50","Application Label" },
        { "87","Application Priority Indicator" },
        { "BF0C","FCI Issuer Discretionary Data" },
        { "9F38","PDOL" },
        { "9F4D","Log Entry" },
        { "88","SFI" },
        { "5F2D","Language Preference" },
        { "9F11","Issuer Code Table Index" },
        { "9F12","Application Preferred Name" },
    };

        /// <summary>
        /// Parses a hexadecimal‐encoded TLV string into a nested Dictionary&lt;string, object&gt;.
        /// Internally, the method:
        ///  1. Cleans the input string of whitespace and hyphens.
        ///  2. Converts every two hex characters into one byte.
        ///  3. Invokes <see cref="ParseTlv(byte[], ref int, int)"/> on the resulting byte array.
        /// </summary>
        /// <param name="hexTlv">
        /// A string containing TLV data represented as hexadecimal characters.
        /// It may include spaces, tabs, newlines, or dashes (“-”) between bytes, all of which will be ignored.
        /// Example: <c>"9F02 06 000000000100"</c> or <c>"9F02-06000000000100"</c>.
        /// </param>
        /// <returns>
        /// A Dictionary mapping each tag name (or raw tag hex if no friendly name exists) to:
        ///  • a nested <see cref="Dictionary{String,Object}"/> for constructed (sub‐TLV) tags, or
        ///  • a <see cref="string"/> (hex‐encoded) for primitive tag values.
        /// </returns>
        /// <exception cref="FormatException">
        /// Thrown if the cleaned hex string has an odd length (cannot form complete bytes),
        /// or if any two‐character chunk is not a valid hex byte.
        /// Any FormatException thrown by <see cref="HexStringToBytes"/> bubbles up here.
        /// </exception>
        public static Dictionary<string, object> Parse(string hexTlv)
        {
            byte[] data = HexStringToBytes(hexTlv);
            int idx = 0;
            return ParseTlv(data, ref idx, data.Length);
        }

        /// <summary>
        /// Parses a raw byte array containing TLV‐encoded data into a nested Dictionary&lt;string, object&gt;.
        /// </summary>
        /// <param name="data">
        /// A byte array where each element represents one byte of TLV‐encoded data.
        /// This method starts at index 0 and attempts to consume the entire array as TLV.
        /// </param>
        /// <returns>
        /// A Dictionary where each key is the tag name (or raw tag hex if no friendly name exists)
        /// and each value is either:
        /// • A nested Dictionary&lt;string, object&gt; for constructed (sub‐TLV) tags, or
        /// • A string (hex‐encoded) for primitive tag values.
        /// </returns>
        public static Dictionary<string, object> Parse(byte[] data)
        {
            int idx = 0;
            return ParseTlv(data, ref idx, data.Length);
        }

        /// <summary>
        /// Serializes a nested Dictionary&lt;string, object&gt; (as produced by Parse or ParseTlv)
        /// into a prettified JSON string with indentation.
        /// </summary>
        /// <param name="dict">
        /// The Dictionary&lt;string, object&gt; to serialize. Each value may itself be another Dictionary&lt;string, object&gt;
        /// or a primitive string. The serializer will automatically recurse into nested dictionaries.
        /// </param>
        /// <returns>
        /// A JSON‐formatted string with indented formatting, making the structure human‐readable.
        /// Example output:
        /// {
        ///   "TagName1": "A1B2C3",
        ///   "TagName2": {
        ///     "SubTag1": "00FF"
        ///   }
        /// }
        /// </returns>
        public static string ToJson(Dictionary<string, object> dict) =>
            JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });

        /// <summary>
        /// Parses a TLV (Tag-Length-Value) buffer beginning at index <paramref name="i"/> and ending at <paramref name="limit"/>.
        /// If a tag is “constructed” (bit‐5 of the first tag byte is set), its value is interpreted as a nested TLV sequence.
        /// Otherwise, the value is returned as a hexadecimal string.
        /// </summary>
        /// <param name="buf">The complete TLV‐encoded byte array.</param>
        /// <param name="i">
        /// Reference to the current read index in <paramref name="buf"/>. 
        /// On return, <paramref name="i"/> will point immediately past the last byte consumed for this TLV block.
        /// </param>
        /// <param name="limit">
        /// The exclusive upper bound inside <paramref name="buf"/> up to which parsing is valid.
        /// Must be between 0 and buf.Length. 
        /// </param>
        /// <returns>
        /// A Dictionary mapping tag names (or raw tag hex strings if not found in TagNames) to either:
        /// • string – for primitive (non‐constructed) tag values, encoded as a hex string without “-” separators 
        /// • Dictionary&lt;string,object&gt; – for constructed tags, representing a nested TLV subtree.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="i"/> or <paramref name="limit"/> are outside the valid range of <paramref name="buf"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown whenever the TLV structure is invalid. Examples:
        /// • Unexpected end of buffer while reading a tag or length
        /// • Invalid length (negative or exceeding available bytes)
        /// • Any error thrown by ReadTag or ReadLength is wrapped in a FormatException with context.
        /// </exception>
        public static Dictionary<string, object> ParseTlv(byte[] buf, ref int i, int limit)
        {
            if (buf == null)
                throw new ArgumentNullException(nameof(buf));
            if (i < 0 || i > buf.Length)
                throw new ArgumentOutOfRangeException(nameof(i), "Start index is outside the buffer bounds.");
            if (limit < 0 || limit > buf.Length)
                throw new ArgumentOutOfRangeException(nameof(limit), "Limit is outside the buffer bounds.");
            if (i > limit)
                throw new ArgumentOutOfRangeException(nameof(limit), "Start index cannot exceed limit.");

            var map = new Dictionary<string, object>();

            while (i < limit)
            {
                // Step 1: Read the Tag
                if (i >= limit)
                    throw new FormatException($"Unexpected end of data at index {i} while expecting a tag.");

                string tag;
                try
                {
                    tag = ReadTag(buf, ref i);
                }
                catch (Exception ex)
                {
                    throw new FormatException($"Error reading tag at index {i}: {ex.Message}", ex);
                }

                // Step 2: Read the Length
                if (i >= limit)
                    throw new FormatException($"Unexpected end of data at index {i} while expecting length for tag '{tag}'.");

                int len;
                try
                {
                    len = ReadLength(buf, ref i);
                }
                catch (Exception ex)
                {
                    throw new FormatException($"Error reading length for tag '{tag}' at index {i}: {ex.Message}", ex);
                }

                if (len < 0)
                    throw new FormatException($"Invalid length {len} for tag '{tag}' at index {i} (length cannot be negative).");

                if (i + len > limit)
                    throw new FormatException(
                        $"Length {len} for tag '{tag}' at index {i} exceeds available data. " +
                        $"Buffer has {limit - i} bytes remaining.");

                // Step 3: Extract the Value
                byte[] valueBytes = buf.AsSpan(i, len).ToArray();
                i += len;

                // Determine if this Tag is “constructed” (bit 5 of the first tag byte is set)
                bool constructed;
                try
                {
                    byte firstTagByte = Convert.ToByte(tag.Substring(0, 2), 16);
                    constructed = (firstTagByte & 0x20) != 0;
                }
                catch (Exception ex)
                {
                    throw new FormatException($"Error interpreting tag '{tag}' as hex at index {i - len}: {ex.Message}", ex);
                }

                if (constructed)
                {
                    // Parse nested TLV recursively
                    int subIndex = 0;
                    Dictionary<string, object> nestedMap;
                    try
                    {
                        nestedMap = ParseTlv(valueBytes, ref subIndex, valueBytes.Length);
                    }
                    catch (Exception ex) when (ex is ArgumentOutOfRangeException || ex is FormatException)
                    {
                        throw new FormatException($"Error parsing constructed value for tag '{tag}': {ex.Message}", ex);
                    }

                    // Ensure we consumed exactly 'len' bytes inside the nested buffer
                    if (subIndex != valueBytes.Length)
                    {
                        throw new FormatException(
                            $"Nested TLV for tag '{tag}' did not consume all {valueBytes.Length} bytes " +
                            $"(consumed {subIndex} bytes).");
                    }

                    map[tag] = nestedMap;
                }
                else
                {
                    // Primitive tag: store hex string (without separators)
                    string hexString = BitConverter.ToString(valueBytes).Replace("-", "");
                    map[tag] = hexString;
                }
            }

            // After collecting raw tag=>value entries, translate tag hex codes to friendly names if possible
            // TagNames is assumed to be a static dictionary in this class or elsewhere in scope:
            //      static readonly Dictionary<string, string> TagNames = ...

            return map.Select(pair =>
                       {
                           string translatedKey = TagNames.ContainsKey(pair.Key)
                           ? $"{TagNames[pair.Key]}({pair.Key})"
                           : pair.Key;
                           return (Key: translatedKey, Value: pair.Value);
                       }).ToDictionary(entry => entry.Key, entry => entry.Value);
        }

        /// <summary>
        /// Reads one TLV tag starting at index <paramref name="indexRef"/> in <paramref name="buffer"/>.
        /// Advances <paramref name="indexRef"/> by the number of bytes consumed for the tag.
        /// 
        /// Throws an exception if the buffer does not contain a complete tag.
        /// </summary>
        /// <param name="buffer">The TLV buffer.</param>
        /// <param name="indexRef">Reference to the current index; updated to point just past the tag.</param>
        /// <returns>The tag as an uppercase hex string (e.g. "5F2A" or "9F1E").</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if there is not enough data at <paramref name="indexRef"/> to read a complete tag.
        /// </exception>
        private static string ReadTag(byte[] buffer, ref int indexRef)
        {
            if (indexRef >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(indexRef), "No data available to read a tag.");

            // First byte:
            byte first = buffer[indexRef++];
            bool moreTagBytes = (first & 0x1F) == 0x1F; // if lower 5 bits are all 1, tag is multi-byte
            var tagBytes = new List<byte> { first };

            if (moreTagBytes)
            {
                // Read subsequent tag bytes until the MSB is zero
                do
                {
                    if (indexRef >= buffer.Length)
                        throw new ArgumentOutOfRangeException(nameof(indexRef), "Truncated tag: missing subsequent tag byte.");
                    byte next = buffer[indexRef++];
                    tagBytes.Add(next);
                    moreTagBytes = (next & 0x80) != 0; // check MSB of each subsequent byte
                }
                while (moreTagBytes);
            }

            // Convert collected bytes to uppercase hex string
            return BitConverter.ToString(tagBytes.ToArray()).Replace("-", "");
        }

        /// <summary>
        /// Reads the length field of a TLV at <paramref name="indexRef"/> in <paramref name="buffer"/>.
        /// Advances <paramref name="indexRef"/> by the number of bytes consumed for length.
        /// 
        /// Supports:
        /// • Short form (one byte, 0x00..0x7F)
        /// • Long form (first byte 0x81..0xFF: bit‐8 set, lower 7 bits indicate number of subsequent length bytes)
        /// Throws an exception if the buffer is truncated or if the length is malformed.
        /// </summary>
        /// <param name="buffer">The TLV buffer.</param>
        /// <param name="indexRef">Reference to the current index; updated to point just past the length field.</param>
        /// <returns>The integer length of the subsequent value field.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if there is not enough data in <paramref name="buffer"/> to read the complete length bytes.
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown if the length’s long‐form indicates an invalid number of length bytes or results in a length that is too large.
        /// </exception>
        private static int ReadLength(byte[] buffer, ref int indexRef)
        {
            if (indexRef >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(indexRef), "No data available to read length.");

            byte firstLengthByte = buffer[indexRef++];
            // Short form: 0xxx xxxx => length is that value (0–127)
            if ((firstLengthByte & 0x80) == 0)
            {
                return firstLengthByte;
            }

            // Long form: 1xxx xxxx => lower 7 bits = number of subsequent length bytes
            int numLengthBytes = firstLengthByte & 0x7F;
            if (numLengthBytes == 0)
                throw new FormatException("Indefinite length encoding not supported.");

            if (numLengthBytes > 4)
                throw new FormatException($"Length field too long: {numLengthBytes} bytes (maximum supported is 4 bytes).");

            if (indexRef + numLengthBytes > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(indexRef), "Truncated data: not enough bytes to read long-form length.");

            int lengthValue = 0;
            for (int j = 0; j < numLengthBytes; j++)
            {
                lengthValue = (lengthValue << 8) | buffer[indexRef++];
            }

            if (lengthValue < 0)
                throw new FormatException($"Negative length value decoded: {lengthValue}.");

            return lengthValue;
        }

        /// <summary>
        /// Converts a hexadecimal string into a byte array.
        /// Removes any whitespace characters (spaces, tabs, newlines) and dashes ('-') before parsing.
        /// </summary>
        /// <param name="hex">
        /// A string of hexadecimal characters (must have even length after cleaning).
        /// Examples of valid inputs:
        ///   • <c>"9F0206000000000100"</c>
        ///   • <c>"9F02 06 000000000100"</c>
        ///   • <c>"9F02-06-00000000-0100"</c>
        /// </param>
        /// <returns>
        /// A byte array where each pair of hex characters is parsed into one byte.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="hex"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown if the cleaned hex string has an odd length (cannot form complete bytes),
        /// or if any two-character chunk is not a valid hex byte (e.g. contains non-hex characters).
        /// </exception>
        public static byte[] HexStringToBytes(string hex)
        {
            // 1.  Remove every whitespace character
            string cleaned = string.Concat(hex.Where(c => !(char.IsWhiteSpace(c) || c == '-')));

            // 2.  The remaining length must be even (two chars per byte)
            if (cleaned.Length % 2 != 0)
                throw new FormatException("The hex string must contain an even number of characters.");

            // 3.  Convert each two-character chunk to a byte
            byte[] result = new byte[cleaned.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                string pair = cleaned.Substring(i * 2, 2);
                try
                {
                    result[i] = Convert.ToByte(pair, 16);
                }
                catch (Exception ex)
                {
                    throw new FormatException($"Invalid hex byte '{pair}' at position {i * 2}–{i * 2 + 1}.", ex);
                }
            }

            return result;
        }
    }
}