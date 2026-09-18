extends SceneTree


func _init() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 2:
		push_error("Usage: godot --headless -s extract_near_death_feast_glow.gd -- <game_pck> <output_png>")
		quit(1)
		return
	if not ProjectSettings.load_resource_pack(args[0]):
		quit(1)
		return

	# SOUL_NEXUS 0.111.0: glowie 在图集中未旋转，两个骨骼插槽分别使用普通与加色混合。
	var texture := load("res://animations/monsters/soul_nexus/soulnexus.png") as Texture2D
	var pixels := texture.get_image()
	if pixels.is_compressed():
		pixels.decompress()
	if pixels.get_size() != Vector2i(1063, 656):
		push_error("Soul Nexus atlas changed; recheck the glowie region before extracting.")
		quit(1)
		return
	var glow := pixels.get_region(Rect2i(2, 77, 580, 577))
	DirAccess.make_dir_recursive_absolute(args[1].get_base_dir())
	var error := glow.save_png(args[1])
	print("SOUL_NEXUS_GLOW size=", glow.get_size(), " alpha=", glow.detect_alpha(), " output=", args[1])
	quit(0 if error == OK else 1)
