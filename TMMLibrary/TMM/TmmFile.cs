namespace TMMLibrary.TMM;

public class TmmFile : IEncode
{
    public TmmHeader? Header;
    public ModelInfo[] ModelInfos = [];
    
    public static TmmFile Decode(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);
        return Decode(br);
    }

    public static TmmFile Decode(BinaryReader br)
    {
        var header = TmmHeader.Decode(br);
        var modelInfo = br.DecodeArray((int)header.ModelCount, ModelInfo.Decode);
        DecodeException.ExpectEof(typeof(TmmFile), br.BaseStream);
        return new TmmFile
        {
            Header = header,
            ModelInfos = modelInfo
        };
    }
    
    public void Encode(BinaryWriter bw)
    {
        Header?.Encode(bw);
        bw.EncodeArray(ModelInfos);
    }
}

public class TmmHeader : IEncode
{
    public static readonly string TMM_MAGIC = "BTMM";
    
    public string MagicId { get; set; } = "";
    public uint Unknown1 { get; set; } // always 34
    public ushort Unknown2 { get; set; } // always 22
    public uint DataOffset { get; set; }
    public uint ModelCount { get; set; } // Unsure if this is correct
    public string[] ModelNames { get; set; } = [];
    public ushort HeaderEndBytes { get; set; } // always 2024


    public static TmmHeader Decode(BinaryReader br)
    {
        var magic = new string(br.ReadChars(4));
        if (magic != TMM_MAGIC)
        {
            throw new DecodeException(typeof(TmmHeader), "incorrect magic header");
        }
        var header = new TmmHeader
        {
            MagicId = magic,
            Unknown1 = br.ReadUInt32(),
            Unknown2 = br.ReadUInt16(),
            DataOffset = br.ReadUInt32(),
            ModelCount = br.ReadUInt32()
        };
        header.ModelNames = br.DecodeArray((int)header.ModelCount, r => r.ReadTmString());
        header.HeaderEndBytes = br.ReadUInt16();
        
        return header;
    }

    public void Encode(BinaryWriter bw)
    {
        bw.Write(TMM_MAGIC);
        bw.Write(Unknown1);
        bw.Write(Unknown2);
        bw.Write(DataOffset);
        bw.Write(ModelCount);
        foreach (var s in ModelNames)
        {
            bw.WriteTmString(s);
        }
        bw.Write(HeaderEndBytes);
    }
}

/// <summary>
/// Describes the model information of a .tmm.data file.
/// </summary>
public class ModelInfo : IEncode
{
    public ushort[] Unknown1 { get; set; } = []; // always 2, 2, 6, 14 / 7, 2, 16, 17
    public ushort[] Unknown2 = [];
    public int SomeCount1;
    public int MaterialCount;
    public int SubmaterialCount;
    public int BoneCount { get; set; }
    public uint UnknownCount { get; set; }
    public int AttachPointCount { get; set; }
    public uint VertexCount { get; set; }
    public uint IndexCount { get; set; }
    public uint VertexOffset { get; set; } // Normally 0x00
    public uint IndexOffset { get; set; }
    public uint IndexOffset2 { get; set; } // Same as indexoffset, not sure why exists
    public uint Unknown3 { get; set; } // probably an offset of some sort
    public uint BoneWeightsOffset { get; set; }
    public uint BoneWeightsByteCount { get; set; }
    public uint[] Unknown4 = []; // size of array is 4
    public uint MaskDataOffset;
    public uint MaskDataByteCount;
    // 0x34 bytes = 13 elements
    public float[] Unknown5 = [];
    public TmmAttachPoint[] AttachPoints = [];
    // 0x1c bytes = 7 elements, most are 0 but some have non-zero values that are probably not floats.
    public uint[] Unknown6 = [];
    public uint[] Unknown7 = [];
    public string[] Materials = [];
    // Appears directly after the material and always seems to be "default"
    public string[] Submaterials = [];
    public Bone[] Bones = [];
    public BoneFloats? BoneFloatData = null;
    public uint BoneFooter;
    public SuffixData? Suffix;
    
    public static ModelInfo Decode(BinaryReader br)
    {
        var model = new ModelInfo
        {
            Unknown1 = br.ReadUInt16Array(4),
            Unknown2 = br.ReadUInt16Array(29),
            SomeCount1 = br.ReadInt32(),
            MaterialCount = br.ReadInt32(),
            SubmaterialCount = br.ReadInt32(),
            BoneCount = br.ReadInt32(),
            UnknownCount = br.ReadUInt32(),
            AttachPointCount = br.ReadInt32(),
            VertexCount = br.ReadUInt32(),
            IndexCount = br.ReadUInt32(),
            VertexOffset = br.ReadUInt32(),
            IndexOffset = br.ReadUInt32(),
            IndexOffset2 = br.ReadUInt32(),
            Unknown3 = br.ReadUInt32(),
            BoneWeightsOffset = br.ReadUInt32(),
            BoneWeightsByteCount = br.ReadUInt32(),
            Unknown4 = br.ReadUInt32Array(4),
            MaskDataOffset = br.ReadUInt32(),
            MaskDataByteCount = br.ReadUInt32(),
        };
        // skip a byte to align. This is probably meaningful?
        br.ReadByte();
        // Another 0x34 bytes worth of floats
        model.Unknown5 = br.ReadFloat32Array(0x34 / 4);
        // Read all attach points
        model.AttachPoints = br.DecodeArray(model.AttachPointCount, TmmAttachPoint.Decode);
        // see tmm_file.hexpat for more details.
        model.Unknown6 = br.ReadUInt32Array(4);
        if (model.SomeCount1 == 2)
        {
            model.Unknown7 = br.ReadUInt32Array(6);
        }
        // Now positioned right before the material name
        model.Materials = br.DecodeArray(model.MaterialCount, IoExtensions.ReadTmString);
        model.Submaterials = br.DecodeArray(model.SubmaterialCount, IoExtensions.ReadTmString);
        // Read all the bones, because the bones are important!
        model.Bones = br.DecodeArray(model.BoneCount, Bone.Decode);
        if (model.BoneCount > 0)
        {
            model.BoneFloatData = BoneFloats.Decode(br);
        }
        else
        {
            // No bones, so skip 4 bytes
            br.ReadUInt32();
        }
        model.BoneFooter = br.ReadUInt32();
        DecodeException.ExpectEqual<uint>(
            typeof(ModelInfo), br.BaseStream.Position, 0x01585600, model.BoneFooter);
        // Skip 3 bytes
        br.ReadBytes(3);
        var moreData = br.ReadBoolean();
        if (moreData)
        {
            model.Suffix = SuffixData.Decode(br);
        }
        return model;
    }

    public void Encode(BinaryWriter bw)
    {
        bw.Write(Unknown1);
        bw.Write(Unknown2);
        bw.Write(BoneCount);
        bw.Write(UnknownCount);
        bw.Write(AttachPointCount);
        bw.Write(VertexCount);
        bw.Write(IndexCount);
        bw.Write(VertexOffset);
        bw.Write(IndexOffset);
        bw.Write(IndexOffset2);
        bw.Write(Unknown3);
        bw.Write(BoneWeightsOffset);
        bw.Write(BoneWeightsByteCount);
        bw.Write(Unknown4);
        bw.Write(MaskDataOffset);
        bw.Write(MaskDataByteCount);
        bw.Write((byte)0);
        bw.Write(Unknown5);
        bw.EncodeArray(AttachPoints);
        bw.Write(Unknown6);
        if (SomeCount1 == 2)
        {
            bw.Write(Unknown7);
        }
        foreach (var v in Materials) bw.WriteTmString(v);
        foreach (var v in Submaterials) bw.WriteTmString(v);
        bw.EncodeArray(Bones);
        if (BoneCount > 0)
        {
            BoneFloatData?.Encode(bw);
        }
        else
        {
            bw.Write((uint)0);
        }
        bw.Write(BoneFooter);
        bw.WriteZeros(3);
        if (Suffix != null)
        {
            bw.Write((byte)1);
            Suffix.Encode(bw);
        }
        else
        {
            bw.Write((byte)0);
        }
    }
}

/// <summary>
/// Not as good as Root Beer floats!
/// </summary>
public class BoneFloats : IEncode
{
    public Half[] Data0 = [];
    public Half[] Data1 = [];
    public Half[] Data2 = [];
    public Half[] Data3 = [];

    public static BoneFloats Decode(BinaryReader br)
    {
        var counts = br.ReadUInt16Array(4);
        return new BoneFloats
        {
            Data0 = br.ReadFloat16Array(counts[0] * 4),
            Data1 = br.ReadFloat16Array(counts[1] * 1),
            Data2 = br.ReadFloat16Array(counts[2] * 16),
            Data3 = br.ReadFloat16Array(counts[3] * 21),
        };
    }

    public void Encode(BinaryWriter bw)
    {
        bw.Write((ushort)(Data0.Length / 4));
        bw.Write((ushort)Data1.Length);
        bw.Write((ushort)(Data2.Length / 16));
        bw.Write((ushort)(Data3.Length / 21));
        bw.Write(Data0);
        bw.Write(Data1);
        bw.Write(Data2);
        bw.Write(Data3);
    }
}

public class SuffixData : IEncode
{
    public ushort ConstVS;
    public uint ByteCount;
    public uint[] Data;

    public static SuffixData Decode(BinaryReader br)
    {
        var myType = typeof(SuffixData);
        var obj = new SuffixData();
        obj.ConstVS = br.ReadUInt16();
        DecodeException.ExpectEqual<ushort>(myType, br.BaseStream.Position, 0x5356, obj.ConstVS);
        obj.ByteCount = br.ReadUInt32();
        obj.Data = br.ReadUInt32Array((int)obj.ByteCount / 4);
        return obj;
    }

    public void Encode(BinaryWriter bw)
    {
        bw.Write(ConstVS);
        bw.Write(ByteCount);
        bw.Write(Data);
    }
}

public class TmmAttachPoint : IEncode
{
    public int Flags;
    public string Name1 = "";
    // Second name, usually the same as the initial name when present but not always present.
    public string Name2 = "";
    public float[] Floats1 = [];
    
    public static TmmAttachPoint Decode(BinaryReader br)
    {
        var myType = typeof(TmmAttachPoint);
        var apoint = new TmmAttachPoint();
        
        // Expect 2x uint32 that are 0, then a flags uint32, then the name
        var zero0 = br.ReadUInt64();
        DecodeException.ExpectEqual(myType, br.BaseStream.Position, zero0, (ulong)0);
        apoint.Flags = br.ReadInt32();
        apoint.Name1 = br.ReadTmString();
        // Start with 0x60 bytes of floats
        apoint.Floats1 = br.ReadFloat32Array(0x60 / 4);
        // Read 2x int32, should be 0
        var zero1 = br.ReadUInt64();
        DecodeException.ExpectEqual(myType, br.BaseStream.Position, zero1, (ulong)0);
        // Next value is always a TmString, but it may be zero-length.
        apoint.Name2 = br.ReadTmString();
        // Ends with [-1, 0, 0]
        var tmp1 = br.ReadUInt32Array(3);
        DecodeException.ExpectEqualList<uint>(myType, br.BaseStream.Position, [0xffffffff, 0, 0], tmp1);
        return apoint;
    }

    public void Encode(BinaryWriter bw)
    {
        bw.Write((ulong)0);
        bw.Write(Flags);
        bw.WriteTmString(Name1);
        bw.Write(Floats1);
        bw.Write((ulong)0); // 8 bytes of 0s
        bw.WriteTmString(Name2);
        // So far, these are constant
        bw.Write(-1);
        bw.Write(0);
        bw.Write(0);
    }
}

public class Bone : IEncode
{
    public string Name = "";
    public int BoneParent;
    public float[] Unknown2 = []; // size = 52

    public static Bone Decode(BinaryReader br)
    {
        return new Bone
        {
            Name = br.ReadTmString(),
            BoneParent = br.ReadInt32(),
            Unknown2 = br.ReadFloat32Array(0xD0 / 4), // = 52
        };
    }
    
    public void Encode(BinaryWriter bw)
    {
        bw.WriteTmString(Name);
        bw.Write(BoneParent);
        bw.Write(Unknown2);
    }
}
