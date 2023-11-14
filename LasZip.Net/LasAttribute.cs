// lasattributer.hpp
using System;
using System.Buffers.Binary;
using System.Text;

namespace LasZip
{
    internal class LasAttribute
    {
        public const int LAS_ATTRIBUTE_U8 = 0;
        public const int LAS_ATTRIBUTE_I8 = 1; // Smallest() and Biggest() rely on signed integers being odd valued types
        public const int LAS_ATTRIBUTE_U16 = 2;
        public const int LAS_ATTRIBUTE_I16 = 3;
        public const int LAS_ATTRIBUTE_U32 = 4;
        public const int LAS_ATTRIBUTE_I32 = 5;
        public const int LAS_ATTRIBUTE_U64 = 6;
        public const int LAS_ATTRIBUTE_I64 = 7;
        public const int LAS_ATTRIBUTE_F32 = 8;
        public const int LAS_ATTRIBUTE_F64 = 9;
        public const int SerializedSizeInBytes = 2 + 1 + 1 + 32 + 4 + 3 * 8 + 3 * 8 + 3 * 8 + 3 * 8 + 3 * 8 + 32; // see field comments below

        public byte[] reserved { get; private init; } // 2 bytes
        public byte data_type { get; set; } // 1 byte
        public byte options { get; set; } // 1 byte, TOOD: make flags enum
        public string name { get; set; } // 32 bytes
        public byte[] unused { get; private init; } // 4 bytes
        public Interpretable64[] no_data { get; private init; } // 24 = 3*8 bytes
        public Interpretable64[] min { get; private init; } // 24 = 3*8 bytes
        public Interpretable64[] max { get; private init; } // 24 = 3*8 bytes
        public double[] scale { get; private init; } // 24 = 3*8 bytes
        public double[] offset { get; private init; } // 24 = 3*8 bytes
        public string? description { get; set; } // 32 bytes

        public LasAttribute(byte size)
            : this(LasAttribute.LAS_ATTRIBUTE_U8, String.Empty, null)
        {
            if (size == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            this.options = size;
        }

        public LasAttribute(byte type, string name, string? description = null)
        {
            if (type > LasAttribute.LAS_ATTRIBUTE_F64)
            {
                throw new ArgumentOutOfRangeException(nameof(type));
            }

            this.reserved = new byte[2];
            this.data_type = type;
            this.name = name;
            this.unused = new byte[4];
            this.no_data = new Interpretable64[3];
            this.min = new Interpretable64[3];
            this.max = new Interpretable64[3];
            this.scale = new double[3] { 1.0, 1.0, 1.0 };
            this.offset = new double[3];
            this.description = description;
        }

        public LasAttribute(LasAttribute other)
            : this(other.data_type, other.name, other.description)
        {
            other.reserved.CopyTo(this.reserved, 0);
            other.no_data.CopyTo(this.no_data, 0);
            other.min.CopyTo(this.min, 0);
            other.max.CopyTo(this.max, 0);
            other.scale.CopyTo(this.scale, 0);
            other.offset.CopyTo(this.offset, 0);
        }

        public LasAttribute(ReadOnlySpan<byte> attributeData)
        {
            if (BitConverter.IsLittleEndian)
            {
                this.reserved = [ attributeData[0], attributeData[1] ];
                this.unused = [ attributeData[35], attributeData[36], attributeData[37], attributeData[38] ];
            }
            else
            {
                this.reserved = [ attributeData[1], attributeData[0] ];
                this.unused = [ attributeData[38], attributeData[37], attributeData[36], attributeData[35] ];
            }

            this.data_type = attributeData[2];
            this.name = Encoding.UTF8.GetString(attributeData.Slice(3, 32));
            this.no_data = [ new(attributeData[39..]), new(attributeData[47..]), new(attributeData[55..]) ];
            this.min = [ new(attributeData[63..]), new(attributeData[71..]), new(attributeData[79..]) ];
            this.max = [ new(attributeData[87..]), new(attributeData[95..]), new(attributeData[103..]) ];
            this.scale = [ BinaryPrimitives.ReadDoubleLittleEndian(attributeData[111..]), BinaryPrimitives.ReadDoubleLittleEndian(attributeData[119..]), BinaryPrimitives.ReadDoubleLittleEndian(attributeData[127..]) ];
            this.offset = [ BinaryPrimitives.ReadDoubleLittleEndian(attributeData[135..]), BinaryPrimitives.ReadDoubleLittleEndian(attributeData[143..]), BinaryPrimitives.ReadDoubleLittleEndian(attributeData[151..]) ];
            this.description = Encoding.UTF8.GetString(attributeData.Slice(159, 32));
        }

        public void copy_to(Span<byte> attributeData)
        {
            if (BitConverter.IsLittleEndian)
            {
                attributeData[0] = this.reserved[0];
                attributeData[1] = this.reserved[1];
                attributeData[35] = this.unused[0];
                attributeData[36] = this.unused[1];
                attributeData[37] = this.unused[2];
                attributeData[38] = this.unused[3];
            }
            else
            {
                attributeData[0] = this.reserved[1];
                attributeData[1] = this.reserved[0];
                attributeData[35] = this.unused[3];
                attributeData[36] = this.unused[2];
                attributeData[37] = this.unused[1];
                attributeData[38] = this.unused[0];
            }

            attributeData[2] = this.data_type;
            Encoding.UTF8.GetBytes(this.name).CopyTo(attributeData.Slice(3, 32));
            BinaryPrimitives.WriteUInt64LittleEndian(attributeData[39..], this.no_data[0].UInt64);
            BinaryPrimitives.WriteUInt64LittleEndian(attributeData[47..], this.no_data[1].UInt64);
            BinaryPrimitives.WriteUInt64LittleEndian(attributeData[55..], this.no_data[2].UInt64);
            BinaryPrimitives.WriteUInt64LittleEndian(attributeData[63..], this.min[0].UInt64);
            BinaryPrimitives.WriteUInt64LittleEndian(attributeData[71..], this.min[1].UInt64);
            BinaryPrimitives.WriteUInt64LittleEndian(attributeData[79..], this.min[2].UInt64);
            BinaryPrimitives.WriteUInt64LittleEndian(attributeData[87..], this.max[0].UInt64);
            BinaryPrimitives.WriteUInt64LittleEndian(attributeData[95..], this.max[1].UInt64);
            BinaryPrimitives.WriteUInt64LittleEndian(attributeData[103..], this.min[2].UInt64);
            BinaryPrimitives.WriteDoubleLittleEndian(attributeData[111..], this.scale[0]);
            BinaryPrimitives.WriteDoubleLittleEndian(attributeData[119..], this.scale[1]);
            BinaryPrimitives.WriteDoubleLittleEndian(attributeData[127..], this.scale[2]);
            BinaryPrimitives.WriteDoubleLittleEndian(attributeData[135..], this.offset[0]);
            BinaryPrimitives.WriteDoubleLittleEndian(attributeData[143..], this.offset[1]);
            BinaryPrimitives.WriteDoubleLittleEndian(attributeData[151..], this.offset[2]);
            Encoding.UTF8.GetBytes(this.description).CopyTo(attributeData.Slice(159, 32));
        }

        public bool set_no_data(byte no_data) { if (0 == get_type()) { this.no_data[0].UInt64 = no_data; options |= 0x01; return true; } return false; }
        public bool set_no_data(sbyte no_data) { if (1 == get_type()) { this.no_data[0].Int64 = no_data; options |= 0x01; return true; } return false; }
        public bool set_no_data(UInt16 no_data) { if (2 == get_type()) { this.no_data[0].UInt64 = no_data; options |= 0x01; return true; } return false; }
        public bool set_no_data(Int16 no_data) { if (3 == get_type()) { this.no_data[0].Int64 = no_data; options |= 0x01; return true; } return false; }
        public bool set_no_data(UInt32 no_data) { if (4 == get_type()) { this.no_data[0].UInt64 = no_data; options |= 0x01; return true; } return false; }
        public bool set_no_data(Int32 no_data) { if (5 == get_type()) { this.no_data[0].Int64 = no_data; options |= 0x01; return true; } return false; }
        public bool set_no_data(UInt64 no_data) { if (6 == get_type()) { this.no_data[0].UInt64 = no_data; options |= 0x01; return true; } return false; }
        public bool set_no_data(Int64 no_data) { if (7 == get_type()) { this.no_data[0].Int64 = no_data; options |= 0x01; return true; } return false; }
        public bool set_no_data(float no_data) { if (8 == get_type()) { this.no_data[0].Double = no_data; options |= 0x01; return true; } return false; }

        public bool set_no_data(double no_data)
        {
            switch (get_type())
            {
                case 0:
                case 2:
                case 4:
                case 6:
                    this.no_data[0].UInt64 = (UInt64)no_data;
                    options |= 0x01;
                    return true;
                case 1:
                case 3:
                case 5:
                case 7:
                    this.no_data[0].Int64 = (Int64)no_data;
                    options |= 0x01;
                    return true;
                case 8:
                case 9:
                    this.no_data[0].Double = no_data;
                    options |= 0x01;
                    return true;
            }
            return false;
        }

        public void set_min(ReadOnlySpan<byte> min) { this.min[0] = cast(min); options |= 0x02; }
        public void update_min(ReadOnlySpan<byte> min) { this.min[0] = smallest(cast(min), this.min[0]); }
        public bool set_min(byte min) { if (0 == get_type()) { this.min[0].UInt64 = min; options |= 0x02; return true; } return false; }
        public bool set_min(sbyte min) { if (1 == get_type()) { this.min[0].Int64 = min; options |= 0x02; return true; } return false; }
        public bool set_min(UInt16 min) { if (2 == get_type()) { this.min[0].UInt64 = min; options |= 0x02; return true; } return false; }
        public bool set_min(Int16 min) { if (3 == get_type()) { this.min[0].Int64 = min; options |= 0x02; return true; } return false; }
        public bool set_min(UInt32 min) { if (4 == get_type()) { this.min[0].UInt64 = min; options |= 0x02; return true; } return false; }
        public bool set_min(Int32 min) { if (5 == get_type()) { this.min[0].Int64 = min; options |= 0x02; return true; } return false; }
        public bool set_min(UInt64 min) { if (6 == get_type()) { this.min[0].UInt64 = min; options |= 0x02; return true; } return false; }
        public bool set_min(Int64 min) { if (7 == get_type()) { this.min[0].Int64 = min; options |= 0x02; return true; } return false; }
        public bool set_min(float min) { if (8 == get_type()) { this.min[0].Double = min; options |= 0x02; return true; } return false; }
        public bool set_min(double min) { if (9 == get_type()) { this.min[0].Double = min; options |= 0x02; return true; } return false; }

        public void set_max(ReadOnlySpan<byte> max) { this.max[0] = cast(max); options |= 0x04; }
        public void update_max(ReadOnlySpan<byte> max) { this.max[0] = biggest(cast(max), this.max[0]); }
        public bool set_max(byte max) { if (0 == get_type()) { this.max[0].UInt64 = max; options |= 0x04; return true; } return false; }
        public bool set_max(sbyte max) { if (1 == get_type()) { this.max[0].Int64 = max; options |= 0x04; return true; } return false; }
        public bool set_max(UInt16 max) { if (2 == get_type()) { this.max[0].UInt64 = max; options |= 0x04; return true; } return false; }
        public bool set_max(Int16 max) { if (3 == get_type()) { this.max[0].Int64 = max; options |= 0x04; return true; } return false; }
        public bool set_max(UInt32 max) { if (4 == get_type()) { this.max[0].UInt64 = max; options |= 0x04; return true; } return false; }
        public bool set_max(Int32 max) { if (5 == get_type()) { this.max[0].Int64 = max; options |= 0x04; return true; } return false; }
        public bool set_max(UInt64 max) { if (6 == get_type()) { this.max[0].UInt64 = max; options |= 0x04; return true; } return false; }
        public bool set_max(Int64 max) { if (7 == get_type()) { this.max[0].Int64 = max; options |= 0x04; return true; } return false; }
        public bool set_max(float max) { if (8 == get_type()) { this.max[0].Double = max; options |= 0x04; return true; } return false; }
        public bool set_max(double max) { if (9 == get_type()) { this.max[0].Double = max; options |= 0x04; return true; } return false; }

        public bool set_scale(double scale) { if (data_type != 0) { this.scale[0] = scale; options |= 0x08; return true; } return false; }
        public bool set_offset(double offset) { if (data_type != 0) { this.offset[0] = offset; options |= 0x10; return true; } return false; }

        public bool unset_scale() { if (data_type != 0) { options &= 0xf7; return true; } return false; }
        public bool unset_offset() { if (data_type != 0) { options &= 0xef; return true; } return false; }

        public bool has_no_data() { return (options & 0x01) != 0; }
        public bool has_min() { return (options & 0x02) != 0; }
        public bool has_max() { return (options & 0x04) != 0; }
        public bool has_scale() { return (options & 0x08) != 0; }
        public bool has_offset() { return (options & 0x10) != 0; }

        public UInt32 get_size()
        {
            if (data_type != 0)
            {
                Span<UInt32> size_table = stackalloc UInt32[] { 1, 1, 2, 2, 4, 4, 8, 8, 4, 8 };
                Int32 type = get_type();
                Int32 dim = get_dim();
                return (UInt32)(size_table[type] * dim);
            }
            else
            {
                return options;
            }
        }

        public double get_value_as_float(ReadOnlySpan<byte> pointer)
        {
            double cast_value = this.get_type() switch
            {
                LasAttribute.LAS_ATTRIBUTE_U8 => pointer[0],
                LasAttribute.LAS_ATTRIBUTE_I8 => (sbyte)pointer[0],
                LasAttribute.LAS_ATTRIBUTE_U16 => BinaryPrimitives.ReadUInt16LittleEndian(pointer),
                LasAttribute.LAS_ATTRIBUTE_I16 => BinaryPrimitives.ReadInt16LittleEndian(pointer),
                LasAttribute.LAS_ATTRIBUTE_U32 => BinaryPrimitives.ReadUInt32LittleEndian(pointer),
                LasAttribute.LAS_ATTRIBUTE_I32 => BinaryPrimitives.ReadInt32LittleEndian(pointer),
                LasAttribute.LAS_ATTRIBUTE_U64 => BinaryPrimitives.ReadUInt64LittleEndian(pointer),
                LasAttribute.LAS_ATTRIBUTE_I64 => BinaryPrimitives.ReadInt64LittleEndian(pointer),
                LasAttribute.LAS_ATTRIBUTE_F32 => BinaryPrimitives.ReadSingleLittleEndian(pointer),
                LasAttribute.LAS_ATTRIBUTE_F64 => BinaryPrimitives.ReadDoubleLittleEndian(pointer),
                _ => throw new NotSupportedException("Unhandled type " + this.get_type() + ".")
            };
            
            if ((options & 0x08) != 0)
            {
                if ((options & 0x10) != 0)
                {
                    return offset[0] + scale[0] * cast_value;
                }
                else
                {
                    return scale[0] * cast_value;
                }
            }
            else
            {
                if ((options & 0x10) != 0)
                {
                    return offset[0] + cast_value;
                }
                else
                {
                    return cast_value;
                }
            }
        }

        public void set_value_as_float(Span<byte> pointer, double value)
        {
            double unoffset_and_unscaled_value;
            if ((options & 0x08) != 0)
            {
                if ((options & 0x10) != 0)
                {
                    unoffset_and_unscaled_value = (value - offset[0]) / scale[0];
                }
                else
                {
                    unoffset_and_unscaled_value = value / scale[0];
                }
            }
            else
            {
                if ((options & 0x10) != 0)
                {
                    unoffset_and_unscaled_value = value - offset[0];
                }
                else
                {
                    unoffset_and_unscaled_value = value;
                }
            }

            switch (this.get_type())
            {
                case LasAttribute.LAS_ATTRIBUTE_U8: 
                    pointer[0] = MyDefs.QuantizeUInt8(unoffset_and_unscaled_value);
                    break;
                case LasAttribute.LAS_ATTRIBUTE_I8:
                    pointer[0] = (byte)MyDefs.QuantizeInt8(unoffset_and_unscaled_value);
                    break;
                case LasAttribute.LAS_ATTRIBUTE_U16: 
                    BinaryPrimitives.WriteUInt16LittleEndian(pointer, MyDefs.QuantizeUInt16(unoffset_and_unscaled_value));
                    break;
                case LasAttribute.LAS_ATTRIBUTE_I16:
                    BinaryPrimitives.WriteInt16LittleEndian(pointer, MyDefs.QuantizeInt16(unoffset_and_unscaled_value));
                    break;
                case LasAttribute.LAS_ATTRIBUTE_U32:
                    BinaryPrimitives.WriteUInt32LittleEndian(pointer, MyDefs.QuantizeUInt32(unoffset_and_unscaled_value));
                    break;
                case LasAttribute.LAS_ATTRIBUTE_I32:
                    BinaryPrimitives.WriteInt32LittleEndian(pointer, MyDefs.QuantizeInt32(unoffset_and_unscaled_value));
                    break;
                case LasAttribute.LAS_ATTRIBUTE_U64:
                    BinaryPrimitives.WriteUInt64LittleEndian(pointer, MyDefs.QuantizeUInt64(unoffset_and_unscaled_value));
                    break;
                case LasAttribute.LAS_ATTRIBUTE_I64:
                    BinaryPrimitives.WriteInt64LittleEndian(pointer, MyDefs.QuantizeInt64(unoffset_and_unscaled_value));
                    break;
                case LasAttribute.LAS_ATTRIBUTE_F32:
                    BinaryPrimitives.WriteSingleLittleEndian(pointer, (float)unoffset_and_unscaled_value);
                    break;
                case LasAttribute.LAS_ATTRIBUTE_F64:
                    BinaryPrimitives.WriteDoubleLittleEndian(pointer, unoffset_and_unscaled_value);
                    break;
                default:
                    throw new NotSupportedException("Unhandled type " + this.get_type() + ".");
            }
        }

        private Int32 get_type()
        {
            return ((Int32)data_type - 1) % 10;
        }

        private Int32 get_dim() // compute dimension of deprecated tuple and triple attributes 
        {
            return ((Int32)data_type - 1) / 10 + 1;
        }

        private Interpretable64 cast(ReadOnlySpan<byte> pointer)
        {
            Interpretable64 cast_value = new();
            switch (this.get_type())
            {
                case LasAttribute.LAS_ATTRIBUTE_U8:
                    cast_value.UInt64 = pointer[0];
                    break;
                case LasAttribute.LAS_ATTRIBUTE_I8:
                    cast_value.Int64 = (sbyte)pointer[0];
                    break;
                case LasAttribute.LAS_ATTRIBUTE_U16:
                    cast_value.UInt64 = BinaryPrimitives.ReadUInt16LittleEndian(pointer);
                    break;
                case LasAttribute.LAS_ATTRIBUTE_I16:
                    cast_value.Int64 = BinaryPrimitives.ReadInt16LittleEndian(pointer);
                    break;
                case LasAttribute.LAS_ATTRIBUTE_U32:
                    cast_value.UInt64 = BinaryPrimitives.ReadUInt32LittleEndian(pointer);
                    break;
                case LasAttribute.LAS_ATTRIBUTE_I32:
                    cast_value.Int64 = BinaryPrimitives.ReadInt32LittleEndian(pointer);
                    break;
                case LasAttribute.LAS_ATTRIBUTE_U64:
                    cast_value.UInt64 = BinaryPrimitives.ReadUInt64LittleEndian(pointer);
                    break;
                case LasAttribute.LAS_ATTRIBUTE_I64:
                    cast_value.Int64 = BinaryPrimitives.ReadInt64LittleEndian(pointer);
                    break;
                case LasAttribute.LAS_ATTRIBUTE_F32:
                    cast_value.Double = BinaryPrimitives.ReadSingleLittleEndian(pointer);
                    break;
                case LasAttribute.LAS_ATTRIBUTE_F64:
                    cast_value.Double = BinaryPrimitives.ReadDoubleLittleEndian(pointer);
                    break;
                default:
                    throw new NotSupportedException("Unhandled type " + this.get_type() + ".");
            }

            return cast_value;
        }

        private Interpretable64 smallest(Interpretable64 a, Interpretable64 b)
        {
            Int32 type = get_type();
            if (type >= 8) // float compare
            {
                if (a.Double < b.Double) 
                    return a;
                else 
                    return b;
            }
            else if ((type & 0x1) != 0) // int compare
            {
                if (a.Int64 < b.Int64) 
                    return a;
                else 
                    return b;
            }
            else if (a.UInt64 < b.UInt64) 
                return a;
            else
                return b;
        }

        private Interpretable64 biggest(Interpretable64 a, Interpretable64 b)
        {
            Int32 type = get_type();
            if (type >= 8) // float compare
            {
                if (a.Double > b.Double) return a;
                else return b;
            }
            if ((type & 0x1) != 0) // int compare
            {
                if (a.Int64 > b.Int64) return a;
                else return b;
            }
            if (a.UInt64 > b.UInt64) 
                return a;
            else
                return b;
        }
    }
}
