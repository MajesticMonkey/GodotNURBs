@tool
extends EditorPlugin

var BezIcon = load("res://addons/nurbs/Textures/NURBIcon.png")

var ControlPointScript = load("res://addons/nurbs/Scripts/ControlPoint.cs")

var NURBBuilderScript = load("res://addons/nurbs/Scripts/NURBBuilder.cs")

var ControlPointInspector

var LoadingBar

func _enter_tree() -> void:
	add_custom_type("ControlPoint", "MeshInstance3D", ControlPointScript, BezIcon)

	add_custom_type("NURBBuilder", "Node3D", NURBBuilderScript, BezIcon)

	LoadingBar = preload("res://addons/nurbs/Scripts/builder_inspector_features.gd").new()
	add_inspector_plugin(LoadingBar)

	ControlPointInspector = preload("res://addons/nurbs/Scripts/control_point_inspector_features.gd").new()
	add_inspector_plugin(ControlPointInspector)

func _exit_tree() -> void:
	remove_custom_type("NURBBuilder")

	remove_custom_type("ControlPoint")

	remove_inspector_plugin(LoadingBar)

	remove_inspector_plugin(ControlPointInspector)
