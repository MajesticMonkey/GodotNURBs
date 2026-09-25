#include "gdnurbs.h"
#include "controlpoint.h"

#include <godot_cpp/core/class_db.hpp>

#include <godot_cpp/classes/array_mesh.hpp>
#include <godot_cpp/classes/collision_shape3d.hpp>
#include <godot_cpp/classes/concave_polygon_shape3d.hpp>
#include <godot_cpp/classes/editor_interface.hpp>
#include <godot_cpp/classes/editor_selection.hpp>
#include <godot_cpp/classes/engine.hpp>
#include <godot_cpp/classes/material.hpp>
#include <godot_cpp/classes/mesh.hpp>
#include <godot_cpp/classes/node.hpp>
#include <godot_cpp/classes/resource_saver.hpp>
#include <godot_cpp/classes/scene_tree.hpp>
#include <godot_cpp/classes/sphere_mesh.hpp>

#include <godot_cpp/variant/dictionary.hpp>
#include <godot_cpp/variant/packed_vector2_array.hpp>
#include <godot_cpp/variant/packed_vector3_array.hpp>
#include <godot_cpp/variant/packed_vector4_array.hpp>
#include <godot_cpp/variant/utility_functions.hpp>
#include <godot_cpp/variant/vector2i.hpp>
#include <godot_cpp/variant/vector2.hpp>
#include <godot_cpp/variant/vector3.hpp>
#include <godot_cpp/variant/vector4.hpp>

#include <cmath>
#include <Eigen/Dense>
#include <future>


using namespace godot;

void NURB::_bind_methods (

)
{
    ClassDB::bind_method(D_METHOD("SetSceneSaveNetwork", "CN"), &NURB::SetSceneSaveNetwork);
    ClassDB::bind_method(D_METHOD("GetSceneSaveNetwork"), &NURB::GetSceneSaveNetwork);

    ADD_PROPERTY(
        PropertyInfo(Variant::PACKED_VECTOR4_ARRAY, "SceneSaveNetwork"),
        "SetSceneSaveNetwork", "GetSceneSaveNetwork");

    ClassDB::bind_method(D_METHOD("SetLoDDetails", "Values"), &NURB::SetLoDDetails);
    ClassDB::bind_method(D_METHOD("GetLoDDetails"), &NURB::GetLoDDetails);

    ADD_PROPERTY(
        PropertyInfo(Variant::DICTIONARY, "Levels of Detail", PROPERTY_HINT_DICTIONARY_TYPE, "3:;2:"),
        "SetLoDDetails", "GetLoDDetails");

    ClassDB::bind_method(D_METHOD("SetPED", "Value"), &NURB::SetPED);
    ClassDB::bind_method(D_METHOD("GetPED"), &NURB::GetPED);

    ADD_PROPERTY(PropertyInfo(Variant::BOOL, "Preserve Edge Detail"), "SetPED", "GetPED");
}

void NURB::SetLoDDetails ( const godot::Dictionary &Values ) {
    LoDDetails = Values;
    
    godot::Array keys = LoDDetails.keys();

    for (int i = 0; i < keys.size(); i++) {
        double key = 1;
        int value = 3;
        
        if (keys[i].get_type() == Variant::FLOAT || keys[i].get_type() == Variant::INT) {
            key = keys[i];
        }
        
        if (LoDDetails[keys[i]].get_type() == Variant::INT) {
            value = LoDDetails[keys[i]];
        }

        if ( abs(key) <= 0.01 ) {
            LoDDetails.erase(key);
            UtilityFunctions::push_warning("You cannot have a key <= 0.0");
            break;
        }
        if ( value < 2 ) {
            LoDDetails[key] = 2;
        }
    }

    // MAKE THIS FUNCTION RELOAD THE SURFACE, BUT MAKE IT NOT DO IT WHEN THIS FUNCTION IS CALLED BEFORE ENTER TREE, SINCE THERE AREN'T ANY DETAILS YET ABOUT HOW TO GENERATE THE MESH
}
godot::Dictionary NURB::GetLoDDetails ( ) const { return LoDDetails; }

void NURB::SetPED ( const bool &PED ) {
    PreserveEdgeDetail = PED;
    // MAKE THIS FUNCTION RELOAD THE SURFACE, BUT MAKE IT NOT DO IT WHEN THIS FUNCTION IS CALLED BEFORE ENTER TREE, SINCE THERE AREN'T ANY DETAILS YET ABOUT HOW TO GENERATE THE MESH
}
bool NURB::GetPED ( ) const { return PreserveEdgeDetail; }

void NURB::SetSceneSaveNetwork ( const PackedVector4Array &Network ) { SceneSaveNetwork = Network; }
godot::PackedVector4Array NURB::GetSceneSaveNetwork ( ) const { return SceneSaveNetwork; }





NURB::NURB (

)
{
    CNMat->set_flag(godot::StandardMaterial3D::FLAG_DISABLE_DEPTH_TEST, true);

    set_process_internal(true);

    CPMesh.instantiate();

    for(int i = 0; i < 4; i++) {
        CPNetwork[i] = Eigen::Matrix<float, 4, 4>::Zero();
    }
}

void NURB::_enter_tree (

)
{
    if (!Engine::get_singleton()->is_editor_hint()) {
        return;
    }

    EditorInterface *ei = EditorInterface::get_singleton();
    if (ei && ei->get_selection() && !ei->get_selection()->is_connected("selection_changed", callable_mp(this, &NURB::_selection_changed))) {
        ei->get_selection()->connect("selection_changed", callable_mp(this, &NURB::_selection_changed));
    }
}

void NURB::_exit_tree (

)
{

}

void NURB::_notification (
    int what
)
{
    switch ( what ) {
        case NOTIFICATION_EDITOR_PRE_SAVE:
            HideChildrenFromSaving();
            break;
	    case NOTIFICATION_EDITOR_POST_SAVE:
            if ( Selected ) {
                UnhideChildrenFromSaving();
            }
            break;
    }
}

void NURB::UpdateSceneSaveNetwork (
    std::array<Eigen::Matrix<float, 4, 4>, 4> CN
)
{
    godot::PackedVector4Array SSNForklift = PackedVector4Array();
    for (int i = 0; i < 16; i++)
    {
        Vector2i Crds = Vector2i(floor((float)i/4.0f), i % 4);
        SSNForklift.append(Vector4(CN[0](Crds.x, Crds.y), CN[1](Crds.x, Crds.y), CN[2](Crds.x, Crds.y), CN[3](Crds.x, Crds.y)));
    }

    SceneSaveNetwork = SSNForklift;
}



void NURB::_selection_changed (

)
{
    EditorInterface *ei = EditorInterface::get_singleton();
    if (!ei) return;

    TypedArray<Node> selection = ei->get_selection()->get_selected_nodes();
    Selected = false;
    if (selection.size() > 0) {
        for (int i = 0; i < selection.size() and !Selected; i++) {
            if (Object::cast_to<Node>(selection[i])->get_instance_id() == get_instance_id()) {
                EnableChildren();
                Selected = true;
            }
            for (int j = 0; j < 16 and !Selected; j++) {
                if (Object::cast_to<Node>(selection[i])->get_instance_id() == CPStorage[j]->get_instance_id()) {
                    EnableChildren();
                    Selected = true;
                }
            }
        }
        if (!Selected) {
            DisableChildren();
        }
    }
    else if (ChildrenEnabled)
    {
        DisableChildren();
    }
}

void NURB::_validate_property(
    godot::PropertyInfo &p_property
) const
{
    if (p_property.name == godot::StringName("mesh")
    or p_property.name == godot::StringName("skeleton")
    or p_property.name == godot::StringName("skin")
    or p_property.name == godot::StringName("SceneSaveNetwork")) {
        p_property.usage &= ~godot::PROPERTY_USAGE_EDITOR;
    }
}

void NURB::_ready (

)
{
    if (!SceneSaveNetwork.is_empty())
    {
        for (int i = 0; i < 16; i++) {
            Vector2i Crd = Vector2i(floor((float)i / 4.0f), i % 4);
            CPNetwork[0](Crd.x, Crd.y) = SceneSaveNetwork[i].x;
            CPNetwork[1](Crd.x, Crd.y) = SceneSaveNetwork[i].y;
            CPNetwork[2](Crd.x, Crd.y) = SceneSaveNetwork[i].z;
            CPNetwork[3](Crd.x, Crd.y) = SceneSaveNetwork[i].w;
        }
    }
    else
    {
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                CPNetwork[0](i, j) = (i / 3.0f) * (float)VPS;
                CPNetwork[1](i, j) = 0;
                CPNetwork[2](i, j) = (j / 3.0f) * (float)VPS;
                CPNetwork[3](i, j) = 1.0f;
            }
        }
    }

    ReloadSurface();

    CreateChildren();

    add_child(StaticBody);

    StaticBody->add_child(CollisionShape);
}

void NURB::_process (
    double delta
)
{
    if (Selected && AutoUpdate)
    {
        bool changed = false;
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                ControlPoint* ControlPointI = CPStorage[i * 4 + j];
                Vector4 CPVec4 = Vector4(
                    ControlPointI->get_position().x,
                    ControlPointI->get_position().y,
                    ControlPointI->get_position().z,
                    ControlPointI->get_weight()
                );
                if (
                    CPNetwork[0](i, j) != CPVec4.x ||
                    CPNetwork[1](i, j) != CPVec4.y ||
                    CPNetwork[2](i, j) != CPVec4.z ||
                    CPNetwork[3](i, j) != CPVec4.w
                )
                {
                    changed = true;
                    CPNetwork[0](i, j) = CPVec4.x;
                    CPNetwork[1](i, j) = CPVec4.y;
                    CPNetwork[2](i, j) = CPVec4.z;
                    CPNetwork[3](i, j) = CPVec4.w;
                }
            }
        }
        if (changed)
        {
            ReloadSurface();

            set_material_override(SurfaceMat);
        }
    }
}

NURB::~NURB(

)
{

}

void NURB::EnableChildren (

)
{
    if (!ChildrenEnabled) {
        for(int i = 0; i < 16; i++){
            add_child(CPStorage[i]);
            CPStorage[i]->set_owner(get_tree()->get_edited_scene_root());
        }
        ChildrenEnabled = true;
    }
}



void NURB::DisableChildren (

)
{
    if (ChildrenEnabled) {
        for(int i = 0; i < 16; i++){
            remove_child(CPStorage[i]);
        }
        ChildrenEnabled = false;
    }
}

void NURB::HideChildrenFromSaving (

)
{
    if (ChildrenEnabled) {
        for(int i = 0; i < 16; i++){
            CPStorage[i]->set_owner(nullptr);
        }
    }
}

void NURB::UnhideChildrenFromSaving (

)
{
    if (ChildrenEnabled) {
        for(int i = 0; i < 16; i++){
            CPStorage[i]->set_owner(get_tree()->get_edited_scene_root());
        }
    }
}

void NURB::CreateChildren (

)
{
    godot::TypedArray<Node> children = get_children();
    if (children.size() > 0) {
        for (int i = 0; i < children.size(); i++) {
            Node3D *node = Object::cast_to<Node3D>(children[i]);
            if (node != nullptr && node->get_name().begins_with(Prefix)) {
                int u = godot::String(node->get_name())[Prefix.length()];
                int v = godot::String(node->get_name())[Prefix.length() + 2];
                CPNetwork[0](u, v) = node->get_position().x;
                CPNetwork[1](u, v) = node->get_position().y;
                CPNetwork[2](u, v) = node->get_position().z;
            }
        }
        
    }
    else
    {
        for(int i = 0; i < 16; i++){
            CPStorage[i] = CreateControlPoint(Vector2i(floor(i / 4), i % 4));
        }
    }
}

godot::ControlPoint* NURB::CreateControlPoint(
    Vector2i UV
)
{
    godot::ControlPoint* ControlPointI = memnew(godot::ControlPoint);

    ControlPointI->set_material_override(CNMat);

    ControlPointI->set_mesh(CPMesh);

    ControlPointI->set_name(Prefix + String::num(UV.x, 0) + "_" + String::num(UV.y, 0));

    ControlPointI->set_position(Vector3(CPNetwork[0](UV.x, UV.y), CPNetwork[1](UV.x, UV.y), CPNetwork[2](UV.x, UV.y)));

    ControlPointI->set_loc(UV);

    return ControlPointI;
}

void NURB::ReloadSurface(

)
{

    UpdateSceneSaveNetwork(CPNetwork);
    
    MeshData meshdata = std::async(std::launch::async, &NURB::IterateOverParametricPoints, this, CPNetwork, B, DB, VPS).get();

    MeshShape.instantiate();

    godot::Array surfacearray;

    surfacearray.resize(Mesh::ARRAY_MAX);


    surfacearray[Mesh::ARRAY_TEX_UV] = godot::Variant(StraightenTriangles<Vector2>(meshdata.uvs, VPS)); // UVs
    surfacearray[Mesh::ARRAY_VERTEX] = godot::Variant(StraightenTriangles<Vector3>(meshdata.positions, VPS)); // Transforms
    surfacearray[Mesh::ARRAY_NORMAL] = godot::Variant(StraightenTriangles<Vector3>(meshdata.normals, VPS)); // Normals
    surfacearray[Mesh::ARRAY_INDEX] = godot::Variant(ConstructLoD(VPS, 1, true));

    godot::Dictionary LoDDictionary = ConstructLoDDictionary(VPS, LoDDetails, PreserveEdgeDetail);

    godot::Array BlendShapes;

    MeshShape->add_surface_from_arrays(Mesh::PRIMITIVE_TRIANGLES, surfacearray, BlendShapes, LoDDictionary);

    set_mesh(MeshShape);
    
    godot::Ref<ConcavePolygonShape3D> ConcaveShape = MeshShape->create_trimesh_shape();
    
    CollisionShape->set_shape(ConcaveShape);
}

template <typename T>
std::conditional_t<std::is_same_v<T, Vector3>, godot::PackedVector3Array, godot::PackedVector2Array>
NURB::StraightenTriangles(
    std::vector<T> verticies,
    int VPS
)
{
    using PackType = std::conditional_t<
        std::is_same_v<T, Vector3>, godot::PackedVector3Array,
        godot::PackedVector2Array
    >;

    PackType PackRAT; // Packed Reordered Array of Triangles

    for (int i = 0; i < VPS; i++) {
        for (int j = 0; j < VPS; j++) {
            PackRAT.append(verticies[i + j * VPS]);
        }
    }

    return PackRAT;
}

godot::PackedInt32Array NURB::WindIndexes( // Deprecated, Remove function when certain it won't be used again.
    int VPS
)
{

    godot::PackedInt32Array PackRAT; // Packed Reordered Array of Triangles

    for (int i = 0; i < VPS - 1; i++) {
        for (int j = 0; j < VPS - 1; j++) {
            PackRAT.append(i + j * VPS);
            PackRAT.append((i + 1) + j * VPS);
            PackRAT.append((i + 1) + (j + 1) * VPS);

            PackRAT.append((i + 1) + (j + 1) * VPS);
            PackRAT.append(i + (j + 1) * VPS);
            PackRAT.append(i + j * VPS);
        }
    }

    return PackRAT;
}

godot::Dictionary NURB::ConstructLoDDictionary(
    int VPS, // Verticies per surface
    godot::Dictionary LoDeets,
    bool PED
)
{
    godot::Array keys = LoDeets.keys();

    godot::Dictionary LoDDictionary = {};
    for (int i = 0; i < keys.size(); i++) {
        LoDDictionary[keys[i]] = ConstructLoD(VPS, LoDeets[keys[i]], PED);
    }

    return LoDDictionary;
}

godot::PackedInt32Array NURB::ConstructLoD(
    int VPS,
    int KeepFreq,
    bool PED
)
{
    godot::PackedInt32Array PackRAT;

    int Edge = VPS - 1;

    
    for (int i = 0; i < VPS; i++) {
        for (int j = 0; j < VPS; j++) {
            if (PED && KeepFreq != 1) {
            if (i == 0 || j == 0 || i == Edge || j == Edge) { // EDGE cases, get it? 
                if (j == VPS - 1 && i != VPS - 1) {
                    PackRAT.append(i + (j * VPS)); // Self
                    PackRAT.append(ClosestKeptPoint(KeepFreq, VPS, i, j));
                    PackRAT.append((i + 1) + (j * VPS));
                } else if (i == 0) {
                    PackRAT.append(i + (j * VPS)); // Self
                    PackRAT.append(ClosestKeptPoint(KeepFreq, VPS, i, j));
                    PackRAT.append((j + 1) * VPS);
                }
                
                if (i == VPS - 1 && j != VPS - 1) {
                    PackRAT.append(ClosestKeptPoint(KeepFreq, VPS, i, j));
                    PackRAT.append(i + (j * VPS)); // Self
                    PackRAT.append(i + ((j + 1) * VPS));
                } else if (j == 0) {
                    PackRAT.append(ClosestKeptPoint(KeepFreq, VPS, i, j));
                    PackRAT.append(i + (j * VPS)); // Self
                    PackRAT.append((i + 1) + (j * VPS));
                }
            } else if (i % KeepFreq == 0 && j % KeepFreq == 0) {
                if (j + KeepFreq < Edge) {
                    if (i + KeepFreq >= Edge) {
                        PackRAT.append(i + (j * VPS)); // Self
                        PackRAT.append((Edge) + ((j + (ceil(static_cast<float>(KeepFreq) / 2.0f)))) * VPS);
                        PackRAT.append(i + ((j + KeepFreq) * VPS)); // Vertically sequential kept vertex
                    }
                    if (i - KeepFreq == 0) {
                        PackRAT.append(i + (j * VPS)); // Self
                        PackRAT.append(i + ((j + KeepFreq) * VPS)); // Vertically sequential kept vertex
                        PackRAT.append((j + (ceil(static_cast<float>(KeepFreq) / 2.0f))) * VPS);

                    }
                }
                if (i + KeepFreq < Edge) {
                    if (j + KeepFreq >= Edge) {
                        PackRAT.append(i + (j * VPS)); // Self
                        PackRAT.append((i + KeepFreq) + (j * VPS)); // Horizontally sequential kept vertex
                        if (KeepFreq == 1) {
                            PackRAT.append((i) + ((j + 1) * VPS));
                        } else {
                            PackRAT.append((i + (ceil((static_cast<float>(KeepFreq) / 2.0f)))) + ((Edge) * VPS));
                        }
                    }
                    if (j - KeepFreq == 0) {
                        PackRAT.append(i + (j * VPS)); // Self
                        PackRAT.append(i + (ceil(static_cast<float>(KeepFreq) / 2.0f)));
                        PackRAT.append((i + KeepFreq) + (j * VPS)); // Vertically sequential kept vertex
                    }
                }
                if (i + KeepFreq < Edge && j + KeepFreq < Edge) {
                    PackRAT.append(i + (j * VPS)); // Self
                    PackRAT.append((i + KeepFreq) + (j * VPS)); // Horizontally sequential kept vertex
                    PackRAT.append(i + ((j + KeepFreq) * VPS)); // Vertically sequential kept vertex
                }
                if (i - KeepFreq > 0 && j - KeepFreq > 0) {
                    PackRAT.append(i + (j * VPS)); // Self
                    PackRAT.append((i - KeepFreq) + (j * VPS)); // Horizontally unsequential kept vertex
                    PackRAT.append(i + ((j - KeepFreq) * VPS)); // Vertically unsequential kept vertex
                }
            }
            } else {
                if (i % KeepFreq == 0 && j % KeepFreq == 0 && i != VPS - 1 && j != VPS - 1) {
                    int iinc = KeepFreq;
                    int jinc = KeepFreq;

                    if (i + KeepFreq > VPS - 1) { iinc = (VPS - 1) - i; }
                    if (j + KeepFreq > VPS - 1) { jinc = (VPS - 1) - j; }

                    PackRAT.append(i + j * VPS);
                    PackRAT.append((i + iinc) + j * VPS);
                    PackRAT.append((i + iinc) + (j + jinc) * VPS);

                    PackRAT.append((i + iinc) + (j + jinc) * VPS);
                    PackRAT.append(i + (j + jinc) * VPS);
                    PackRAT.append(i + j * VPS);
                }
                
            }
        }
    }

    return PackRAT;

    /*
    NOTES:

    If we start to have performance issues, this function is a prime option to optimize. 
    */
}

int NURB::ClosestKeptPoint(
    int KeepFreq,
    int VPS,
    int i,
    int j
)
{
    int n;
    int m;
    
    if (i == 0) {
        n = KeepFreq;
    } else {
        float np = (static_cast<float>(i) / static_cast<float>(KeepFreq));
        if (i - KeepFreq < 0) {
            np = ceil(np);
        } else if (round(np) * KeepFreq >= (VPS - 1)) {
            np = floor(np);
        } else {
            np = round(np);
        }
        n = np * KeepFreq;
    }


    
    if (j == 0) {
        m = KeepFreq;
    } else {
        float mp = static_cast<float>(j) / static_cast<float>(KeepFreq);
        if (j - KeepFreq < 0) {
            mp = ceil(mp);
        } else if(round(mp) * KeepFreq >= (VPS - 1)) {
            mp = floor(mp);
        } else {
            mp = round(mp);
        }
        m = mp * KeepFreq;
    }

    return n + (m * VPS);
}

NURB::MeshData NURB::IterateOverParametricPoints(
    const std::array<Eigen::Matrix<float, 4, 4, 0, 4, 4>, 4> CN,
    const Eigen::Matrix<float, 4, 4, 0, 4, 4> B,
    const Eigen::Matrix<float, 4, 4, 0, 4, 4> DB,
    const int VertexDensity
)
{
    std::array<Eigen::Matrix<float, 4, 4, 0, 4, 4>, 4> CNWeighted;
    for (int i = 0; i < 4; i++) {
        for (int j = 0; j < 4; j++) {
            CNWeighted[0](i, j) = CN[0](i, j) * CN[3](i, j);
            CNWeighted[1](i, j) = CN[1](i, j) * CN[3](i, j);
            CNWeighted[2](i, j) = CN[2](i, j) * CN[3](i, j);
            CNWeighted[3](i, j) = CN[3](i, j);
        }
    }

    MeshData PositionsAndNormals;
    for (int u = 0; u < VertexDensity; u++) {
        float uF = (float)u / (VertexDensity - 1.0f);
        for (int v = 0; v < VertexDensity; v++) {
            float vF = (float)v / ((float)VertexDensity - 1.0f);

           
            Vector4 ForkliftT = ComputeParametricPoint(uF, vF, CNWeighted, B, B, VertexDensity);
            Vector4 ForkliftA = ComputeParametricPoint(uF, vF, CNWeighted, DB, B, VertexDensity);
            Vector4 ForkliftB = ComputeParametricPoint(uF, vF, CNWeighted, B, DB, VertexDensity);

            Vector3 TangentA = Vector3((ForkliftA.x * ForkliftT.w) - (ForkliftA.w * ForkliftT.x), (ForkliftA.y * ForkliftT.w) - (ForkliftA.w * ForkliftT.y), (ForkliftA.z * ForkliftT.w) - (ForkliftA.w * ForkliftT.z));
            Vector3 TangentB = Vector3((ForkliftB.x * ForkliftT.w) - (ForkliftB.w * ForkliftT.x), (ForkliftB.y * ForkliftT.w) - (ForkliftB.w * ForkliftT.y), (ForkliftB.z * ForkliftT.w) - (ForkliftB.w * ForkliftT.z));
            
            PositionsAndNormals.uvs.emplace_back(Vector2(uF, vF));
            PositionsAndNormals.positions.emplace_back(Vector3(ForkliftT.x / ForkliftT.w, ForkliftT.y / ForkliftT.w, ForkliftT.z / ForkliftT.w));
            PositionsAndNormals.normals.emplace_back((TangentA.cross(TangentB)).normalized());
        }
    }
    return PositionsAndNormals;
}

Vector4 NURB::ComputeParametricPoint (
    float u,
    float v,
    std::array<Eigen::Matrix<float, 4, 4, 0, 4, 4>, 4> CNW,
    Eigen::Matrix<float, 4, 4, 0, 4, 4> NB,
    Eigen::Matrix<float, 4, 4, 0, 4, 4> MB,
    int VertexDensity
)
{
    Eigen::Vector4f pu;
    Eigen::Vector4f pv;

    pu << 1.0f, u, pow(u, 2.0f), pow(u, 3.0f);
    pv << 1.0f, v, pow(v, 2.0f), pow(v, 3.0f);
    
    Vector4 Point = Vector4(0.0f, 0.0f, 0.0f, 0.0f);

    Eigen::Matrix<float, 1, 4> pub = (MB * pu).transpose();
    Eigen::Matrix<float, 4, 1> bpv = (NB * pv);

    Point.x = pub.dot(CNW[0] * bpv);
    Point.y = pub.dot(CNW[1] * bpv);
    Point.z = pub.dot(CNW[2] * bpv);
    Point.w = pub.dot(CNW[3] * bpv);

    return Point;
}