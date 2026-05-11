@tool
extends EditorInspectorPlugin


func _can_handle(object: Object) -> bool:
	return object is ControlPoint


func _parse_begin(object: Object) -> void:
	var Builder = object.get_parent()

	var surface_button = Button.new()
	if object.HasSurface:
		surface_button.text = "Remove Surface Here"
		surface_button.pressed.connect(object.RemoveSurface)
		add_custom_control(surface_button)
	else:
		surface_button.text = "Add Surface Here"
		surface_button.pressed.connect(object.CreateSurface)
		add_custom_control(surface_button)

func _parse_property(object: Object, type: Variant.Type, name: String, hint_type: PropertyHint, hint_string: String, usage_flags: int, wide: bool) -> bool:
	if name == "position":
		return false
	return true
