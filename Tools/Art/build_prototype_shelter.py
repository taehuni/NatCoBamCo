import bpy, math, random, json, os
from mathutils import Vector
ROOT=r'C:/Users/PC/UnityProject/LastShelter'
OUT=ROOT+'/Assets/04_Assets/PrototypeShelterV1'
SRC=ROOT+'/ArtSource/PrototypeShelterV1'
random.seed(8)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for m in list(bpy.data.materials): bpy.data.materials.remove(m)
palette={'Concrete':('AAB0AA',0,.88),'DarkConcrete':('525C59',0,.9),'Olive':('586651',.22,.68),'Steel':('35423F',.65,.4),'Black':('20292B',.2,.72),'Rust':('8E5138',.35,.75),'Safety':('DFAF45',.15,.6),'Cloth':('626A53',0,.94),'Pants':('353E3E',0,.92),'Leather':('4A352C',0,.85),'Skin':('B79270',0,.8),'Glass':('527D86',.6,.22),'Glow':('8FE0CD',.2,.28),'Ivory':('D7D0B6',0,.75),'Brick':('885443',0,.92),'Wood':('8B7455',0,.88)}
mats={}
def material(name):
 h,metal,rough=palette[name]; color=tuple(((int(h[i:i+2],16)/255+.055)/1.055)**2.4 for i in (0,2,4));m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;p=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None) or m.node_tree.nodes.new('ShaderNodeBsdfPrincipled');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
 if name=='Glow':p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=1.2
 return m
for name in palette:mats[name]=material(name)
objects=[]; groups={}; current=''; weights={}
def begin(name):
 global objects,current,weights
 objects=[];weights={};current=name

def finish_obj(o,name,mat,bone=None):
 o.name=name;o.data.materials.append(mats[mat]);objects.append(o)
 if bone:weights[o.name]=bone
 return o

def box(name,loc,scale,mat='Steel',bev=.025,bone=None):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bev:
  mod=o.modifiers.new('Rounded edges','BEVEL');mod.width=bev;mod.segments=2;bpy.ops.object.modifier_apply(modifier=mod.name)
  mod=o.modifiers.new('Weighted corners','WEIGHTED_NORMAL');bpy.ops.object.modifier_apply(modifier=mod.name)
 return finish_obj(o,name,mat,bone)

def sphere(name,loc,scale,mat='Cloth',bone=None):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=10,radius=1,location=loc);o=bpy.context.object;o.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 for p in o.data.polygons:p.use_smooth=True
 return finish_obj(o,name,mat,bone)

def rod(name,a,b,r,mat='Steel',bone=None,vertices=12):
 a,b=Vector(a),Vector(b);d=b-a;bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=d.length,location=(a+b)/2);o=bpy.context.object;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();return finish_obj(o,name,mat,bone)

def text(name,body,loc,size=.3,mat='Ivory'):
 bpy.ops.object.text_add(location=loc,rotation=(math.pi/2,0,0));o=bpy.context.object;o.data.body=body;o.data.size=size;o.data.align_x='CENTER';o.data.extrude=.002;bpy.ops.object.convert(target='MESH');return finish_obj(o,name,mat)

def export(name,rig=None):
 global objects
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0]
 bpy.ops.object.join();merged=bpy.context.object;merged.name=name+'Mesh';bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');objects=[merged]
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 if rig:rig.select_set(True);bpy.context.view_layer.objects.active=rig
 else:bpy.context.view_layer.objects.active=objects[0]
 bpy.ops.export_scene.fbx(filepath=OUT+'/Models/'+name+'.fbx',use_selection=True,object_types={'MESH','ARMATURE'} if rig else {'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,bake_anim=bool(rig),bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True)
 groups[name]=list(objects)+([rig] if rig else [])
 for o in groups[name]:o.hide_set(True);o.hide_render=True

def bolts_panel(x,y,z,w,h):
 for dx in [-w/2+.05,w/2-.05]:
  for dz in [-h/2+.05,h/2-.05]:rod('Panel bolt',(x+dx,y-.02,z+dz),(x+dx,y-.045,z+dz),.024,'Safety',vertices=6)

def crate(loc=(0,0,0),size=1):
 x,y,z=loc;box('Crate body',(x,y,z+.45*size),(1.1*size,.8*size,.9*size),'Olive',.04)
 for dz in [.08,.82]:box('Crate rim',(x,y,z+dz*size),(1.16*size,.86*size,.09*size),'Steel')
 for dx in [-.38,.38]:box('Latch strap',(x+dx*size,y-.41*size,z+.45*size),(.07*size,.04*size,.75*size),'Safety',.009)
 box('Handle',(x,y-.45*size,z+.6*size),(.25*size,.06*size,.07*size),'Black')

begin('Survivor')
# Human scale: 1.86m; forward -Y. Clothes are weighted rigidly to anatomical bones.
box('Jacket torso',(0,0,1.28),(.48,.3,.43),'Cloth',.085,'Spine')
sphere('Shoulder mantle',(0,.025,1.48),(.3,.19,.13),'Olive','Spine')
box('Waist coat',(0,0,1.035),(.41,.29,.16),'Cloth',.04,'Pelvis')
box('Trouser pelvis',(0,.012,.94),(.37,.25,.2),'Pants',.055,'Pelvis')
box('Utility belt',(0,-.005,1.015),(.44,.31,.065),'Leather',.014,'Pelvis');box('Buckle',(0,-.17,1.015),(.095,.025,.055),'Safety',.01,'Pelvis')
for x in [-.145,.145]:
 box('Vest webbing',(x,-.168,1.31),(.052,.023,.34),'Leather',.008,'Spine');box('Front pouch',(x,-.2,1.22),(.125,.072,.15),'Olive',.02,'Spine');box('Pouch flap',(x,-.243,1.26),(.13,.022,.045),'Cloth',.005,'Spine')
box('Zipper',(0,-.162,1.31),(.014,.018,.36),'Steel',.002,'Spine')
box('Identity patch',(.113,-.208,1.432),(.12,.015,.05),'Ivory',.004,'Spine')
box('Pack',(0,.235,1.3),(.35,.22,.42),'Olive',.06,'Spine');box('Pack pocket',(0,.365,1.24),(.29,.065,.19),'Cloth',.026,'Spine')
for x in [-.115,.115]:box('Pack strap',(x,.352,1.4),(.045,.032,.31),'Leather',.009,'Spine')
rod('Bedroll',(-.24,.24,1.54),(.24,.24,1.54),.095,'Cloth','Spine')
for x in [-.14,.14]:box('Bedroll strap',(x,.24,1.54),(.035,.195,.195),'Leather',.013,'Spine')
rod('Neck',(0,0,1.48),(0,0,1.62),.082,'Skin','Head')
sphere('Head',(0,-.015,1.7),(.12,.113,.15),'Skin','Head')
sphere('Hood',(0,.023,1.724),(.148,.139,.15),'Cloth','Head')
box('Face opening',(0,-.103,1.71),(.195,.04,.165),'Skin',.034,'Head')
box('Goggles frame',(0,-.141,1.751),(.225,.038,.07),'Black',.018,'Head')
for x in [-.058,.058]:box('Goggle lens',(x,-.165,1.751),(.093,.013,.043),'Glass',.009,'Head')
box('Respirator',(0,-.153,1.661),(.158,.075,.09),'Black',.025,'Head')
for x in [-.065,.065]:rod('Respirator filter',(x,-.186,1.65),(x,-.207,1.65),.026,'Steel','Head')
box('Scarf',(0,-.03,1.566),(.27,.25,.07),'Rust',.025,'Head')
# Articulated limbs; arms carry a compact rifle.
for side,s in [('L',-1),('R',1)]:
 hip=(s*.115,0,.96);knee=(s*.12,0,.54);ankle=(s*.12,.018,.16)
 box(side+' Thigh',(s*.115,0,.755),(.21,.235,.45),'Pants',.065,side+'Thigh')
 box(side+' Cargo pouch',(s*.207,-.005,.765),(.08,.14,.16),'Cloth',.019,side+'Thigh')
 box(side+' Calf',(s*.12,.005,.365),(.18,.2,.4),'Pants',.055,side+'Shin')
 box(side+' Kneepad',(s*.12,-.098,.555),(.14,.06,.14),'Steel',.026,side+'Shin')
 box(side+' Boot',(s*.12,-.06,.095),(.19,.34,.17),'Leather',.04,side+'Foot');box(side+' Sole',(s*.12,-.075,.026),(.2,.355,.046),'Black',.01,side+'Foot')
 for z in [.115,.145]:box(side+' Boot lace',(s*.12,-.189,z),(.105,.013,.012),'Ivory',.002,side+'Foot')
 shoulder=(s*.25,0,1.455);elbow=(s*.325,-.095,1.2);hand=(s*.13,-.365,1.24)
 d=Vector(elbow)-Vector(shoulder);o=sphere(side+' Sleeve',(Vector(shoulder)+Vector(elbow))/2,(.108,.108,d.length*.59),'Cloth',side+'Arm');o.rotation_euler=d.to_track_quat('Z','Y').to_euler()
 d=Vector(hand)-Vector(elbow);o=sphere(side+' Forearm',(Vector(hand)+Vector(elbow))/2,(.082,.082,d.length*.6),'Cloth',side+'Forearm');o.rotation_euler=d.to_track_quat('Z','Y').to_euler()
 sphere(side+' Glove',hand,(.067,.086,.063),'Leather',side+'Hand')
 box(side+' Shoulder patch',(s*.322,-.015,1.433),(.027,.1,.09),'Rust',.009,side+'Arm')
# Weapon is a visual child of spine; game weapon logic remains on original object.
box('Rifle receiver',(.015,-.385,1.264),(.085,.35,.115),'Steel',.012,'Spine');box('Rifle stock',(.015,-.15,1.275),(.092,.18,.1),'Black',.018,'Spine')
rod('Barrel',(.015,-.54,1.292),(.015,-.83,1.292),.026,'Black','Spine');rod('Muzzle',(.015,-.8,1.292),(.015,-.875,1.292),.036,'Steel','Spine')
box('Magazine',(.015,-.41,1.152),(.065,.095,.14),'Black',.012,'Spine');box('Sight',(.015,-.39,1.35),(.048,.115,.036),'Black',.007,'Spine')
for y in [-.45,-.49,-.53]:box('Handguard rail',(.015,y,1.324),(.103,.025,.027),'Black',.004,'Spine')
# Armature and three reusable locomotion clips.
bpy.ops.object.armature_add(location=(0,0,0));rig=bpy.context.object;rig.name='SurvivorRig';bpy.ops.object.mode_set(mode='EDIT');rig.data.edit_bones.remove(rig.data.edit_bones[0])
def bone(name,a,b,parent=None):
 e=rig.data.edit_bones.new(name);e.head=a;e.tail=b
 if parent:e.parent=rig.data.edit_bones[parent]
bone('Root',(0,0,0),(0,0,.15));bone('Pelvis',(0,0,.9),(0,0,1.08),'Root');bone('Spine',(0,0,1.08),(0,0,1.51),'Pelvis');bone('Head',(0,0,1.51),(0,0,1.83),'Spine')
for side,s in [('L',-1),('R',1)]:
 bone(side+'Thigh',(s*.115,0,.96),(s*.12,0,.54),'Pelvis');bone(side+'Shin',(s*.12,0,.54),(s*.12,.018,.16),side+'Thigh');bone(side+'Foot',(s*.12,.018,.16),(s*.12,-.2,.08),side+'Shin')
 bone(side+'Arm',(s*.25,0,1.455),(s*.325,-.095,1.2),'Spine');bone(side+'Forearm',(s*.325,-.095,1.2),(s*.13,-.365,1.24),side+'Arm');bone(side+'Hand',(s*.13,-.365,1.24),(s*.13,-.44,1.24),side+'Forearm')
bpy.ops.object.mode_set(mode='OBJECT')
for o in objects:
 vg=o.vertex_groups.new(name=weights.get(o.name,'Spine'));vg.add(list(range(len(o.data.vertices))),1,'REPLACE');mod=o.modifiers.new('Rig','ARMATURE');mod.object=rig;o.parent=rig
for name,swing,length in [('Idle',0,60),('Walk',24,30),('Run',42,20)]:
 rig.animation_data_create();rig.animation_data.action=bpy.data.actions.new(name)
 for pb in rig.pose.bones:pb.rotation_mode='XYZ';pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
 for i in range(9):
  frame=1+i*length/8;phase=i*math.pi/4
  for side,sign in [('L',1),('R',-1)]:
   rig.pose.bones[side+'Thigh'].rotation_euler.x=math.radians(swing)*math.sin(phase)*sign
   rig.pose.bones[side+'Shin'].rotation_euler.x=math.radians(-swing*.95)*max(0,math.sin(phase)*sign)
   rig.pose.bones[side+'Foot'].rotation_euler.x=math.radians(swing*.25)*math.sin(phase)*sign
  rig.pose.bones['Pelvis'].location.y=(.004 if name=='Idle' else .025)*math.sin(phase*2)
  rig.pose.bones['Spine'].rotation_euler.x=math.radians(1.2)*math.sin(phase)+(math.radians(7) if name=='Run' else 0)
  for pb in rig.pose.bones:pb.keyframe_insert('rotation_euler',frame=frame,group=pb.name);pb.keyframe_insert('location',frame=frame,group=pb.name)
 rig.animation_data.action.use_fake_user=True
rig.animation_data.action=None
for pb in rig.pose.bones:pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
export('Survivor',rig)

begin('CoreGenerator')
box('Foundation',(0,0,.13),(3,2.3,.26),'DarkConcrete',.06)
box('Body',(0,0,1.04),(2.6,1.8,1.6),'Olive',.12)
for x in [-1.22,1.22]:box('End guard',(x,0,1.02),(.16,1.91,1.7),'Steel',.035)
box('Top cover',(0,0,1.93),(2.8,1.96,.15),'Steel',.045)
for x in [-.87,-.6,-.33,0,.33]:box('Vent',(x,-.922,1.02),(.085,.055,.95),'Black',.009)
box('Control face',(.86,-.95,1.22),(.48,.12,.64),'Steel',.035);box('Display',(.86,-1.017,1.36),(.32,.018,.19),'Glow',.01)
for x in [.73,.85,.97]:rod('Switch',(x,-1.013,1.12),(x,-1.055,1.12),.035,'Safety')
box('Stripe',(0,-.932,.47),(2.38,.025,.12),'Safety',.003)
for x in [-1,-.75,-.5,-.25,0,.25,.5,.75,1]:o=box('Hazard slash',(x,-.952,.47),(.065,.015,.12),'Black',.001);o.rotation_euler.y=-.35
for x in [-.9,.9]:rod('Exhaust',(x,.55,1.9),(x,.55,2.55),.1,'Steel');box('Exhaust hood',(x,.55,2.6),(.35,.3,.11),'Steel')
text('Core sign','SHELTER // 01',(0,-.934,1.69),.19)
export('CoreGenerator')

def cabin(name,width,depth,body,label):
 begin(name);box('Footing',(0,0,.13),(width+.35,depth+.35,.26),'DarkConcrete',.04);box('Walls',(0,0,1.57),(width,depth,2.9),body,.035)
 for x in [-width/2,width/2]:
  for y in [-depth/2,depth/2]:box('Corner pillar',(x,y,1.63),(.2,.2,3.1),'Concrete')
 box('Roof',(0,0,3.12),(width+.5,depth+.5,.23),'Steel',.035)
 for x in [-width/2+.6,width/2-.6]:
  box('Window recess',(x,-depth/2-.026,1.84),(1,.06,1.1),'Black',.02);box('Window',(x,-depth/2-.068,1.85),(.89,.023,.98),'Glass',.01)
  box('Window divider',(x,-depth/2-.09,1.85),(.05,.03,1),'Steel',.004);box('Sill',(x,-depth/2-.14,1.3),(1.16,.28,.07),'Concrete')
 box('Door frame',(0,-depth/2-.055,1.22),(1.35,.12,2.4),'Steel',.024);box('Door',(0,-depth/2-.128,1.19),(1.13,.03,2.21),'Olive',.015);box('Door upper panel',(0,-depth/2-.15,1.65),(.86,.02,.77),'DarkConcrete',.01);box('Handle',(.42,-depth/2-.2,1.16),(.045,.08,.25),'Safety',.006)
 box('Sign back',(0,-depth/2-.085,2.78),(3.6,.08,.4),'Steel',.008);text('Facility sign',label,(0,-depth/2-.135,2.66),.26)
 for i in range(int(width*3)):
  x=-width/2+.2+i*.33
  box('Panel seam',(x,depth/2+.025,1.58),(.018,.035,2.7),'DarkConcrete',0)
 # drain pipe, ventilation and utility meter
 rod('Drain pipe',(width/2+.09,depth/2-.35,.3),(width/2+.09,depth/2-.35,3.1),.065,'Steel')
 box('External unit',(width/2+.3,0,1.03),(.55,1.2,.85),'Concrete',.04)
 for y in [-.35,-.175,0,.175,.35]:box('Unit grille',(width/2+.59,y,1.04),(.03,.065,.6),'Steel',.004)

cabin('WorkshopModule',7,4.5,'Concrete','WORKSHOP / 01')
for x in [-2.8,2.8]:rod('Canopy post',(x,-3.7,0),(x,-3.7,2.9),.08,'Steel')
box('Canopy',(0,-3.1,2.93),(6.2,2,.14),'Olive')
box('Workbench',(2,-2.9,.89),(1.5,.7,.11),'Wood')
for x in [1.4,2.6]:box('Bench leg',(x,-2.9,.44),(.1,.5,.88),'Steel')
crate((-2.1,-3.05,0),.8);export('WorkshopModule')
cabin('ResearchCabin',6,4,'Olive','RESEARCH / 02')
for x in [-1.8,1.8]:
 o=box('Solar frame',(x,0,3.45),(2,2.5,.1),'Steel');o.rotation_euler.x=.17
 for dx in [-.65,0,.65]:box('Solar cell',(x+dx,-.05,3.53),(.58,2.25,.035),'Glass',.008)
rod('Antenna',(2.4,1.4,3.2),(2.4,1.4,5.1),.035,'Steel');rod('Antenna cross',(1.9,1.4,4.8),(2.9,1.4,4.8),.02,'Steel');export('ResearchCabin')
cabin('ResidenceModule',8,5,'Brick','RESIDENCE / 03')
# Raised brick courses on sides, staggered joints.
for z in [.4,.75,1.1,1.45,1.8,2.15,2.5,2.85]:box('Brick course',(0,2.513,z),(7.8,.027,.024),'Concrete',0)
for i in range(14):box('Roof seam',(-3.8+i*.58,0,3.255),(.026,5.2,.025),'DarkConcrete',0)
export('ResidenceModule')
begin('PerimeterWall')
box('Base',(0,0,.13),(4.05,.72,.26),'DarkConcrete');box('Wall',(0,0,.9),(3.8,.36,1.45),'Concrete',.045)
for x in [-1.94,1.94]:box('Pillar',(x,0,1.25),(.25,.55,2.5),'DarkConcrete',.026);box('Pillar cap',(x,0,2.54),(.36,.64,.12),'Concrete')
for z in [1.68,2.43]:rod('Fence rail',(-1.88,0,z),(1.88,0,z),.034,'Steel')
for i in range(20):
 x=-1.85+i*.19;rod('Fence upright',(x,0,1.7),(x,0,2.42),.012,'Steel',vertices=6)
for z in [1.94,2.18]:rod('Fence wire',(-1.9,0,z),(1.9,0,z),.009,'Steel',vertices=6)
export('PerimeterWall')
begin('Barricade')
box('Concrete block',(0,0,.47),(2.8,.72,.88),'Concrete',.14);box('Foot',(0,0,.1),(3,.95,.2),'DarkConcrete')
box('Hazard band',(0,-.371,.6),(2.5,.025,.2),'Safety',.001)
for x in [-1.05,-.7,-.35,0,.35,.7,1.05]:o=box('Hazard black',(x,-.391,.6),(.14,.015,.2),'Black',.001);o.rotation_euler.y=-.35
for x in [-.95,.95]:rod('Lifting eye',(x,0,.9),(x,0,1.04),.04,'Steel')
export('Barricade')
begin('SupplyCrate');crate();export('SupplyCrate')
begin('LightMast');box('Mast foundation',(0,0,.12),(.65,.65,.24),'Concrete');rod('Mast',(0,0,.2),(0,0,4.8),.085,'Steel');rod('Arm',(0,0,4.7),(0,-.6,4.7),.06,'Steel');box('Lamp housing',(0,-.64,4.6),(.6,.8,.2),'Steel');box('Lamp lens',(0,-.64,4.486),(.48,.65,.026),'Glow',.012);export('LightMast')
# Export material values for deterministic URP remapping.
json.dump({n:{'hex':v[0],'metallic':v[1],'roughness':v[2]} for n,v in palette.items()},open(OUT+'/palette.json','w'),indent=2)
# Contact sheet source file. Keep authoring source outside Assets to avoid automatic .blend import.
for index,(name,obs) in enumerate(groups.items()):
 for o in obs:
  o.hide_set(False);o.hide_render=False
  if o.parent is None:o.location.x+=(index%4)*12;o.location.y+=(index//4)*10
bpy.ops.wm.save_as_mainfile(filepath=SRC+'/PrototypeShelterV1.blend')
print('PROTOTYPE_ASSETS_COMPLETE', {n:sum(len(o.data.polygons) for o in obs if o.type=='MESH') for n,obs in groups.items()})



