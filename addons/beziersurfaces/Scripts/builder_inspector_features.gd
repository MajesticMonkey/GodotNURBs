@tool
extends EditorInspectorPlugin

func _can_handle(object: Object) -> bool:
	return object is BezierSurfaceBuilder

func _parse_begin(object: Object) -> void:
	var reload_button = Button.new()
	reload_button.text = "Reload Surfaces"
	reload_button.pressed.connect(object.UpdateSurfaces)
	add_custom_control(reload_button)

	var reload_all_button = Button.new()
	reload_all_button.text = "Reload All Surfaces"
	reload_all_button.pressed.connect(object.UpdateAllSurfaces)
	add_custom_control(reload_all_button)
