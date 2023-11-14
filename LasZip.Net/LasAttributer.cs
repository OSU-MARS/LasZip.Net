// lasattributer.hpp
using System;

namespace LasZip
{
    internal class LasAttributer
    {
        public bool attributes_linked;
        public UInt32 number_attributes;
        public LasAttribute[]? attributes; // TODO: change all three arrays to to List<T>
        public Int32[]? attribute_starts;
        public Int32[]? attribute_sizes;

        public LasAttributer()
        {
            this.attributes_linked = true;
            this.number_attributes = 0;
            this.attributes = null;
            this.attribute_starts = null;
            this.attribute_sizes = null;
        }

        public void clean_attributes()
        {
            if (this.attributes_linked)
            {
                if (attributes != null)
                {
                    this.number_attributes = 0;
                    this.attributes = null;
                    this.attribute_starts = null;
                    this.attribute_sizes = null;
                }
            }
        }

        public bool init_attributes(UInt32 number_attributes, ReadOnlySpan<byte> attributeData)
        {
            this.clean_attributes();

            this.number_attributes = number_attributes;
            this.attributes = new LasAttribute[number_attributes];
            this.attribute_starts = new Int32[number_attributes];
            this.attribute_sizes = new Int32[number_attributes];

            this.attribute_starts[0] = 0;
            this.attribute_sizes[0] = (int)this.attributes[0].get_size();
            for (int index = 0; index < number_attributes; ++index)
            {
                int attributeStartOffset = this.attribute_starts[index - 1] + this.attribute_sizes[index - 1];
                this.attributes[index] = new(attributeData[attributeStartOffset..]);
                this.attribute_starts[index] = attributeStartOffset;
                this.attribute_sizes[index] = (int)this.attributes[index].get_size();
            }
            return true;
        }

        public Int32 add_attribute(LasAttribute attribute)
        {
            if (attribute.get_size() != 0)
            {
                if (this.attributes != null)
                {
                    this.number_attributes++;
                    Array.Resize(ref this.attributes, (int)this.number_attributes);
                    Array.Resize(ref this.attribute_starts, (int)this.number_attributes);
                    Array.Resize(ref this.attribute_sizes, (int)this.number_attributes);

                    this.attributes[number_attributes - 1] = attribute;
                    this.attribute_starts[number_attributes - 1] = this.attribute_starts[number_attributes - 2] + this.attribute_sizes[number_attributes - 2];
                    this.attribute_sizes[number_attributes - 1] = (int)this.attributes[number_attributes - 1].get_size();
                }
                else
                {
                    this.number_attributes = 1;
                    this.attributes = new LasAttribute[1];
                    this.attribute_starts = new Int32[1];
                    this.attribute_sizes = new Int32[1];
                    this.attributes[0] = attribute;
                    this.attribute_starts[0] = 0;
                    this.attribute_sizes[0] = (int)attributes[0].get_size();
                }

                return (int)(number_attributes - 1);
            }

            return -1;
        }

        public Int16 get_attributes_size()
        {
            return this.attributes != null ? (Int16)(attribute_starts[number_attributes - 1] + attribute_sizes[number_attributes - 1]) : (Int16)0;
        }

        public int get_attribute_index(string name)
        {
            for (int i = 0; i < this.number_attributes; i++)
            {
                if (String.Equals(this.attributes[i].name, name, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        public int get_attribute_start(string name)
        {
            for (int i = 0; i < number_attributes; i++)
            {
                if (String.Equals(attributes[i].name, name, StringComparison.Ordinal))
                {
                    return (int)attribute_starts[i];
                }
            }
            return -1;
        }

        public int get_attribute_start(Int32 index)
        {
            if (index < number_attributes)
            {
                return this.attribute_starts[index];
            }
            return -1;
        }

        public int get_attribute_size(Int32 index)
        {
            if (index < number_attributes)
            {
                return this.attribute_sizes[index];
            }
            return -1;
        }

        public string? get_attribute_name(Int32 index)
        {
            if (index < number_attributes)
            {
                return this.attributes[index].name;
            }
            return null;
        }

        public bool remove_attribute(Int32 index)
        {
            if (index < 0 || index >= number_attributes)
            {
                return false;
            }
            for (index = index + 1; index < number_attributes; index++)
            {
                this.attributes[index - 1] = attributes[index];
                if (index > 1)
                {
                    this.attribute_starts[index - 1] = attribute_starts[index - 2] + attribute_sizes[index - 2];
                }
                else
                {
                    this.attribute_starts[index - 1] = 0;
                }
                this.attribute_sizes[index - 1] = attribute_sizes[index];
            }
            this.number_attributes--;
            if (this.number_attributes != 0)
            {
                Array.Resize(ref this.attributes, (int)this.number_attributes);
                Array.Resize(ref this.attribute_starts, (int)this.number_attributes);
                Array.Resize(ref this.attribute_sizes, (int)this.number_attributes);
            }
            else
            {
                this.attributes = null;
                this.attribute_starts = null;
                this.attribute_sizes = null;
            }
            return true;
        }

        public bool remove_attribute(string name)
        {
            Int32 index = get_attribute_index(name);
            if (index != -1)
            {
                return remove_attribute(index);
            }
            return false;
        }

        public byte[] get_bytes()
        {
            byte[] vlrData = new byte[LasAttribute.SerializedSizeInBytes * this.attributes.Length];
            for (int index = 0; index < this.attributes.Length; ++index)
            {
                this.attributes[index].copy_to(vlrData[(LasAttribute.SerializedSizeInBytes * index)..]);
            }
            return vlrData;
        }
    }
}
