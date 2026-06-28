using System;
using System.Globalization;

namespace McpSdk.Protocol
{
    /// <summary>
    /// A JSON-RPC request id. The specification allows ids to be either a string or a number,
    /// so this value type carries one of the two without losing the distinction (an id received
    /// as a string must be echoed back as a string, and likewise for numbers).
    /// </summary>
    public readonly struct RequestId : IEquatable<RequestId>
    {
        private readonly string _stringValue;
        private readonly long _numberValue;

        public RequestId(string value)
        {
            _stringValue = value ?? throw new ArgumentNullException(nameof(value));
            _numberValue = 0;
            IsString = true;
        }

        public RequestId(long value)
        {
            _stringValue = null;
            _numberValue = value;
            IsString = false;
        }

        /// <summary>True when this id is a string; otherwise it is a number.</summary>
        public bool IsString { get; }

        /// <summary>True when this id is a number; otherwise it is a string.</summary>
        public bool IsNumber => !IsString;

        /// <summary>The string value. Only meaningful when <see cref="IsString"/> is true.</summary>
        public string StringValue => _stringValue;

        /// <summary>The numeric value. Only meaningful when <see cref="IsNumber"/> is true.</summary>
        public long NumberValue => _numberValue;

        /// <summary>Reads a request id from a JSON-RPC <c>id</c> property, preserving its type.</summary>
        public static RequestId FromJson(IJsonProperty property)
        {
            return property.IsString
                ? new RequestId(property.AsString())
                : new RequestId(property.AsLong());
        }

        /// <summary>Writes this id as the named property on the supplied writer, preserving its type.</summary>
        public void WriteTo(IJsonWriter writer, string propertyName)
        {
            if (IsString)
                writer.Write(propertyName, _stringValue);
            else
                writer.Write(propertyName, _numberValue);
        }

        public bool Equals(RequestId other)
        {
            if (IsString != other.IsString)
                return false;

            return IsString
                ? string.Equals(_stringValue, other._stringValue, StringComparison.Ordinal)
                : _numberValue == other._numberValue;
        }

        public override bool Equals(object obj) => obj is RequestId other && Equals(other);

        public override int GetHashCode()
        {
            return IsString
                ? (_stringValue != null ? _stringValue.GetHashCode() : 0)
                : _numberValue.GetHashCode();
        }

        public override string ToString()
        {
            return IsString ? _stringValue : _numberValue.ToString(CultureInfo.InvariantCulture);
        }
    }
}
