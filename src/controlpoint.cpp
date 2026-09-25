#include "controlpoint.h"

#include <godot_cpp/core/class_db.hpp>

#include <godot_cpp/variant/vector2i.hpp>

using namespace godot;

void ControlPoint::_bind_methods (

)
{
    ClassDB::bind_method(D_METHOD("set_weight", "W"), &ControlPoint::set_weight);
    ClassDB::bind_method(D_METHOD("get_weight"), &ControlPoint::get_weight);

    ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "Weight"), "set_weight", "get_weight");
}

void ControlPoint::set_weight ( const float &W ) {
    Weight = W;
    if ( Weight <= 0 ) {
        Weight = .01;
    }
}
float ControlPoint::get_weight ( ) const { return Weight; }

void ControlPoint::set_loc ( const Vector2i &L ) { Loc = L; }
Vector2i ControlPoint::get_loc() const { return Loc; }