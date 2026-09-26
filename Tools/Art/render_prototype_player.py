import bpy, math, os
from mathutils import Vector
root=r'C:/Users/PC/UnityProject/LastShelter'
bpy.ops.wm.open_mainfile(filepath=root+'/ArtSource/PrototypeShelterV1/PrototypeShelterV1.blend')
for o in bpy.data.objects:o.hide_render=o.name not in ['SurvivorMesh','SurvivorRig']
rig=bpy.data.objects['SurvivorRig'];rig.animation_data.action=None
for pb in rig.pose.bones:pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.location.z=-.025
m=bpy.data.materials.new('Studio');m.diffuse_color=(.12,.15,.15,1);floor.data.materials.append(m)
bpy.ops.object.camera_add(location=(2.8,-4.8,2.5));camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,.95))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=2.5;bpy.context.scene.camera=camera
for pos,power,size in [((2,-3,5),650,4),((-3,-1,3),500,3),((1,3,4),900,3)]:
 bpy.ops.object.light_add(type='AREA',location=pos);light=bpy.context.object;light.data.energy=power;light.data.shape='DISK';light.data.size=size;light.rotation_euler=(Vector((0,0,1))-light.location).to_track_quat('-Z','Y').to_euler()
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32;scene.render.resolution_x=900;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.world.color=(.18,.18,.18);scene.render.image_settings.file_format='PNG';os.makedirs(root+'/ArtSource/PrototypeShelterV1/Previews',exist_ok=True);scene.render.filepath=root+'/ArtSource/PrototypeShelterV1/Previews/Survivor.png';bpy.ops.render.render(write_still=True)
