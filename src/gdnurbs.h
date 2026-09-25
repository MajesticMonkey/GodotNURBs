#ifndef GDNURBS_H
#define GDNURBS_H

#include "controlpoint.h"

#include <godot_cpp/core/math.hpp>

#include <godot_cpp/classes/collision_shape3d.hpp>
#include <godot_cpp/classes/material.hpp>
#include <godot_cpp/classes/mesh_instance3d.hpp>
#include <godot_cpp/classes/array_mesh.hpp>
#include <godot_cpp/classes/mesh.hpp>
#include <godot_cpp/classes/sphere_mesh.hpp>
#include <godot_cpp/classes/standard_material3d.hpp>
#include <godot_cpp/classes/static_body3d.hpp>

#include <godot_cpp/variant/array.hpp>
#include <godot_cpp/variant/dictionary.hpp>
#include <godot_cpp/variant/packed_vector2_array.hpp>
#include <godot_cpp/variant/packed_vector3_array.hpp>
#include <godot_cpp/variant/packed_vector4_array.hpp>
#include <godot_cpp/variant/vector2i.hpp>
#include <godot_cpp/variant/vector2.hpp>
#include <godot_cpp/variant/vector3.hpp>


#include <Eigen/Dense>





namespace godot {
    class AbstractNURB : public MeshInstance3D {
        

        private:
            
            
        
        protected:
            
            Ref<godot::ArrayMesh> MeshShape;
            Ref<godot::ConcavePolygonShape3D> MeshCollider;
            Ref<godot::SphereMesh> CPMesh;
            int VPS = 32; // Verticies Per Side
            bool Selected = false;
            bool ChildrenEnabled = false;
            bool Collision = true;
            godot::String Prefix = "ControlPoint_";

        public:
            bool AutoUpdate = true;

            
            
    };

    class NURB : public AbstractNURB {
        GDCLASS(NURB, AbstractNURB);

        private:
            godot::Dictionary LoDDetails;
            bool PreserveEdgeDetail = true;

            
            godot::NodePath XPPath;
            godot::NodePath XNPath;
            godot::NodePath ZPPath;
            godot::NodePath ZNPath;
            godot::NURB* XPNeighbor;
            godot::NURB* XNNeighbor;
            godot::NURB* ZPNeighbor;
            godot::NURB* ZNNeighbor;
            godot::StaticBody3D* StaticBody = memnew(StaticBody3D);
            godot::CollisionShape3D* CollisionShape = memnew(CollisionShape3D);
            Ref<godot::Material> SurfaceMat;
            godot::Ref<godot::StandardMaterial3D> CNMat = memnew(StandardMaterial3D);
            godot::PackedVector4Array SceneSaveNetwork;
            std::array<Eigen::Matrix<float, 4, 4>, 4> CPNetwork; // X, Y, Z, Weight
            std::array<godot::ControlPoint*, 16> CPStorage;
            inline static const Eigen::Matrix<float, 4, 4> B{
                {1, -3, 3, -1},
                {0, 3, -6, 3},
                {0, 0, 3, -3},
                {0, 0, 0, 1}
            };
            inline static const Eigen::Matrix<float, 4, 4> DB{
                {-3, 6, -3, 0},
                {3, -12, 9, 0},
                {0, 6, -9, 0},
                {0, 0, 3, 0}
            };
            
            

            void CreateChildren(

            );

            void EnableChildren(
                
            );

            void DisableChildren(
                
            );

            void HideChildrenFromSaving (

            );

            void UnhideChildrenFromSaving (

            );

            godot::ControlPoint* CreateControlPoint(
                Vector2i UV
            );

            void ReloadSurface(

            );

            template <typename T>
            std::conditional_t<std::is_same_v<T, Vector3>, godot::PackedVector3Array, godot::PackedVector2Array>
            StraightenTriangles(
                std::vector<T> verticies,
                int VPS
            );

            godot::PackedInt32Array WindIndexes( // Deprecated, Remove function when certain it won't be used again.
                int VPS
            );
            
            godot::Dictionary ConstructLoDDictionary(
                int VPS,
                godot::Dictionary LoDeets,
                bool PED
            );

            godot::PackedInt32Array ConstructLoD(
                int VPS,
                int RmvFreq,
                bool PED
            );

            int ClosestKeptPoint(
                int KeepFreq,
                int VPS,
                int i,
                int j
            );

            struct MeshData {
                std::vector<Vector2> uvs;
                std::vector<Vector3> positions;
                std::vector<Vector3> normals;
            };

            MeshData IterateOverParametricPoints(
                const std::array<Eigen::Matrix<float, 4, 4, 0, 4, 4>, 4> CN,
                const Eigen::Matrix<float, 4, 4, 0, 4, 4> B,
                const Eigen::Matrix<float, 4, 4, 0, 4, 4> DB,
                const int VertexDensity
            );

            Vector4 ComputeParametricPoint(
                float u,
                float v,
                std::array<Eigen::Matrix<float, 4, 4, 0, 4, 4>, 4> CNW,
                Eigen::Matrix<float, 4, 4, 0, 4, 4> NB,
                Eigen::Matrix<float, 4, 4, 0, 4, 4> MB,
                int VertexDensity
            );
            
            
    
        protected:
            static void _bind_methods();
            
            void _validate_property(
                godot::PropertyInfo &p_property
            ) const;

        public:
            void _enter_tree() override;
            void _exit_tree() override;
            void _ready() override;
            void _process(double delta) override;
            void _notification(int p_what);

            void _selection_changed();


            void SetLoDDetails(const godot::Dictionary &Values);
            godot::Dictionary GetLoDDetails() const;

            void SetPED ( const bool &PED );
            bool GetPED ( ) const;

            void UpdateSceneSaveNetwork(std::array<Eigen::Matrix<float, 4, 4>, 4> CN);
            void SetSceneSaveNetwork(const godot::PackedVector4Array &Network);
            godot::PackedVector4Array GetSceneSaveNetwork() const;

            void SetXPPath(const godot::NodePath &N);
            godot::NodePath GetXPPath() const;

            void SetXNPath(const godot::NodePath &N);
            godot::NodePath GetXNPath() const;

            void SetZPPath(const godot::NodePath &N);
            godot::NodePath GetZPPath() const;

            void SetZNPath(const godot::NodePath &N);
            godot::NodePath GetZNPath() const;

            void ConnectPath();

            NURB();
            ~NURB();
    };
}

#endif