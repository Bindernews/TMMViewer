#pragma description AoMR .tmm
#pragma magic [ 42 54 4D 4D ] @ 0x00
import std.sys;
import type.float16;

struct TmString {
    u32 len;
    if (len > 100) {
        std::assert_warn(false, "TmString too long");
        return;
    }
    char16 text[len];
} [[single_color]];

struct Bone {
    TmString name;
    s32 parent_id;
    float data[52];
};

struct ZU32 {
    u32 zero;
    if (zero != 0) {
        std::assert_warn(false, "expected value to be 0");
    } else {
        continue;
    }
};

struct AttachPoint {
    u32 unk1[2];
    s32 flags;
    TmString name1;
    float floats1[0x60 / 4];
    ZU32 z2[2];
    // Second name, usually the same as name1 when present, but optional
    TmString name2;
    s32 const_neg1;
    std::assert(const_neg1 == -1, "const_neg1 != -1");
    ZU32 z3[2];
};

struct TmmHeader {
    char magic[4];
    std::assert(magic == "BTMM", "invalid magic");
    u32 unk1;
    std::assert(unk1 == 34, "unk1 wrong");
    u16 magic_dp;
    std::assert_warn(magic_dp == 0x5044, "magic_dp wrong");
    s32 data_offset;
    u32 model_count;
    TmString model_names[model_count];
    u16 const_3;
    std::assert_warn(const_3 == 2024, "const_3 wrong");
};

struct FooterFloats {
    u16 count_0;
    u16 count_1;
    u16 count_2;
    u16 count_3;
    type::float16 data_0[count_0*4];
    type::float16 data_1[count_1];
    type::float16 data_2[count_2*16];
    type::float16 data_3[count_3*21];
};

struct SuffixData {
    u16 const_vs;
    std::assert_warn(const_vs == 0x5356, "expected const 0x5356");
    u32 more_size;
    u32 data[more_size / 4];
};

struct ModelInfo {
    u16 unk1[4];
    u16 unk2[29];
    u32 some_count_1;
    std::assert(some_count_1 <= 2, "some_count_1 > 2, don't know how to parse this");
    u32 material_count;
    u32 submaterial_count;
    u32 bone_count;
    u32 unk_count_1;
    u32 attach_point_count;
    u32 vertex_count;
    u32 index_count;
    u32 vertex_offset;
    u32 index_offset;
    u32 index_offset_copy;
    std::assert(index_offset == index_offset_copy, "index_offset != index_offset_copy");
    u32 unk_offset_1;
    u32 unk_count_2;
    u32 unk_offset_2;
    ZU32 z2[4];
    u32 unk_count_3;
    u32 unk_offset_3;
    u8;
    float unk5_floats[0x34 / 4];
    AttachPoint attach_points[attach_point_count];
    ZU32 z3[3];
    // This is probably an array of some_count_1 * 6 or something, but the numbers
    // don't line up quite right, so for now we just hard-code the skip7 size.
    u32 unk6[4];
    if (some_count_1 == 2) {
        u32 skip7[6];
    }
    TmString materials[material_count];
    TmString submaterials[submaterial_count];
    Bone bones[bone_count];
    // this allows us to see the partially-parsed data even if it errors out
    if (bone_count > 0) {
        FooterFloats unk8;
    } else {
        u32;
    }
    u32 bone_footer;
    std::assert_warn(bone_footer == 0x01585600, "incorrect footer");
    // should be 3 bytes of zeros, then 1 byte boolean. So far the only
    // instance where this is 1 is if bone_count == 0.
    padding[3];
    bool more_data;
    if (more_data) {
        SuffixData suffix;
    }
};

struct TmmFile {
    TmmHeader header;
    ModelInfo models[header.model_count] [[inline]];
};

TmmFile root @ 0;
