extends Control

@onready var n_body_simulation_gpu: Node2D = $"../../Camera/NBodySimulationGPU"
@onready var camera: Camera2D = $"../../Camera"
@onready var G_slider: HSlider = $"G Slider"
@onready var e_slider: HSlider = $"e Slider"
@onready var n_slider: HSlider = $"n Slider"
@onready var t_slider: HSlider = $"t Slider"
@onready var G_label: Label = $"G Slider/Label"
@onready var e_label: Label = $"e Slider/Label"
@onready var n_label: Label = $"n Slider/Label"
@onready var t_label: Label = $"t Slider/Label"

@export var zoom_amount : float = 0.2

func _ready() -> void:
	n_body_simulation_gpu.gravitationalConstant = G_slider.value
	n_body_simulation_gpu.epsilon = e_slider.value
	n_body_simulation_gpu.nextParticleCount = n_slider.value
	n_body_simulation_gpu.timeScale = t_slider.value
	
	G_label.text = str("G: ", G_slider.value)
	e_label.text = str("Epsilon: ", e_slider.value)
	n_label.text = str("n: ", n_slider.value)
	t_label.text = str("t: ", t_slider.value)
	
	n_body_simulation_gpu.Initialize()

func _on_g_slider_value_changed(value: float) -> void:
	n_body_simulation_gpu.gravitationalConstant = value
	G_label.text = str("G: ", value)



func _on_e_slider_value_changed(value: float) -> void:
	n_body_simulation_gpu.epsilon = value
	e_label.text = str("Epsilon: ", value)


func _on_n_slider_value_changed(value: float) -> void:
	n_body_simulation_gpu.nextParticleCount = value
	n_label.text = str("n: ", value)
	
func _on_t_slider_value_changed(value: float) -> void:
	n_body_simulation_gpu.timeScale = value
	t_label.text = str("t: ", value)

func _input(event: InputEvent) -> void:
	if event is InputEventMouseMotion and Input.is_action_pressed("pan"):
		n_body_simulation_gpu.cameraOffset -= event.relative
	if event is InputEventMouseButton:
		if event.is_pressed():
			if event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
				var current_zoom = camera.zoom.x
				camera.zoom = Vector2.ONE * exp(log(current_zoom) - zoom_amount)
			elif event.button_index == MOUSE_BUTTON_WHEEL_UP:
				var current_zoom = camera.zoom.x
				camera.zoom = Vector2.ONE * exp(log(current_zoom) + zoom_amount)
