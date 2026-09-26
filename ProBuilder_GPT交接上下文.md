# Unity / ProBuilder 学习对话交接

整理日期：2026-09-24。请接手的 GPT 先阅读本文，沿用用户版本、中文界面和已验证结论。不要重复已经纠正过的错误。

## 1. 用户习惯与环境

- 用户是新手，要求用最直白的大白话讲解，给清楚的点击位置、模式和操作顺序。
- 使用中文 Unity。菜单说明应写“编辑 → 快捷方式…”等用户实际看得到的名称，包内保留英文的菜单同时给英文。
- 不知道就查官方资料，不要猜按钮、快捷键、顶点编号。
- 除非明确要求修改，否则只讲解，不修改项目。用户本次明确要求整理交接文件，因此创建本文件；此前助手没有修改场景、模型或项目代码。
- 项目：F:\1Learning\unityproject\BamCoNatCo。
- 用户截图显示 Unity 6.3 LTS，6000.3.18f1，Windows，URP。
- manifest 已确认：ProBuilder 6.0.9；ProGrids 3.0.3-preview.6。
- 本地包源码位置：Library/PackageCache/com.unity.probuilder@1f279ab829b7、com.unity.progrids@36b0033bf980。
- 截图中的未保存场景可能比磁盘场景新。不能把磁盘中旧坐标当作截图当前状态。

## 2. 最新问题：Insert Edge Loop 后的分段线怎么合并

用户最新原话：“insert edge loop 的线怎么合并？比如图中三处分开的线”。

最新截图描述：一个较大的平台/长方体，透视俯视。靠近观察者的横向轮廓边，中间长段被选中为黄色，两端各有短黑段；用户分别红圈左端、中间、右端。ProGrids 间距显示 0.5，ON 开启。用户此前正在学习平台四周升墙。

必须区分三种意图，不能看见三段就认定网格裂开：

1. 想让三段一起移动：边模式 Shift 多选三段即可，不需要合并网格。
2. 想消除新增分割：对于同一平面、相邻的面，可在面模式选中后执行 工具 → ProBuilder → Geometry → Merge Faces。它合并面并移除内部划分边；不保证只凭这一操作就消掉所有外轮廓上的共线顶点。不要把顶面和竖直侧面一起合并，也不要把形成墙厚的边框误删。
3. 顶点实际重叠但没连上：顶点模式，选中应当连接的端点，Geometry → Weld Vertices，使用很小的 Weld Distance，例如 0.001。Weld 处理接近/重叠顶点，不是把相隔很远的多段边一键变成长边。不要用很大阈值把整个平台焊塌。

目前截图不足以确认是否有重复顶点、内部交线或只是三段共享端点的边。接手时应优先明确用户希望“一起移动”还是“去掉分割”，必要时让用户提供顶点模式近图。本文不含截图原图，用户可将最新截图一并发给新 GPT。

## 3. 当前平台四周升墙的教学方案

用户希望创建平台，然后四周升起有厚度的墙。

已讲步骤：

1. 游戏对象 → ProBuilder → Cube。
2. 在 ProBuilder Shape 的大小设置 X=10、Y=0.5、Z=10，Transform Scale 保持 1,1,1。
3. 面模式，只选顶面；R 缩放；场景顶部“轴心”切换成“中心”，让缩放围绕选中面的中心。
4. Shift + 缩小顶面形成 Inset，保留中间地板和外围一圈边框。若外轮廓 10×10、内轮廓 9.4×9.4，墙厚是 0.3。
5. 面模式选中外围四块窄面，不要选中间地板，也不要选平台原本竖直的外侧面。
6. 工具 → ProBuilder → Geometry → Extrude，Extrude By 选择 Face Normal / Face Normals，Distance=3。保持相邻面连接，不选 Individual Faces。
7. 得到地板和墙连在一起、顶上敞开的模型。

官方 Inset 文档明确支持 Shift+缩放：也可先沿顶面 X 轴 Shift+缩放，再松开 Shift 沿 Z 缩放。助手还讲过 Shift+拖缩放中心方块的方法，但未在用户编辑器实际测试。后续若用户发现此步骤行为不符，需核对选择模式和缩放轴心，不要坚持错误操作。

用户问过 Center 是什么：场景顶部“轴心”按钮位于“局部/全局”按钮左侧，可切成“中心”。它改变工具手柄的操作参考位置，不会永久修改模型轴心。

## 4. 快捷键：重点纠正

### Bridge Edges

- 官方旧文档和 tooltip 仍写 Alt+B，但本项目 ProBuilder 6.0.9 的菜单注册没有默认 Alt+B 绑定。
- 用户按 Alt+B 没反应；改点菜单后成功。
- 用户打开“编辑 → 快捷方式…”，搜索 Bridge Edges，截图确实出现 Tools/ProBuilder/Geometry/Bridge Edges，类型 Menu，快捷方式栏为空。
- 用户配置名为 Default copy (1)。已告知可以双击快捷方式栏按 Alt+B 自定义；是否最后保留绑定未确认。
- 用户还问过恢复全部默认：切回配置下拉框里的 Default，原自定义配置仍保留。
- 不要再把 tooltip 的按键文字当成实际注册的默认快捷键。必要时查 EditorToolbarMenuItems.cs、Shortcut 属性、用户快捷方式列表。
- 已查源码，Geometry/Extrude 菜单实际带 %E，即默认 Ctrl+E；Weld Vertices 菜单带 &V，即 Alt+V。但仍应尊重用户当前快捷键配置。

### 桥接条件

- 必须在 ProBuilder 上下文、边选择模式。
- 同一个网格上恰好选两条边。
- 默认需要两条开放边，即每条边只邻接一个面。
- 菜单：工具 → ProBuilder → Geometry → Bridge Edges。
- 菜单灰色优先排查模式、数量、是否同一物体；能点但失败，排查是否已经删除开口面、是否误选外框边。

## 5. Cube 镂空矩形洞：用户已成功

用户学会普通 Cube 转 ProBuilder：工具 → ProBuilder → Object → Pro Builderize。转换后能编辑点、边、面，但不自动获得 ProBuilder Shape 的参数化形状设置。

矩形贯通洞教学：

1. 用 Cut 在前、后两个平行面各切一个矩形。
2. 在顶点位置编辑器中用准确坐标使两个矩形前后对应。
3. 面模式删除前后中间的小矩形面。Windows 用 Backspace，或 Geometry → Delete Faces；不要随便按 Delete 删除整个物体。
4. 边模式依次桥接前后洞口的上、下、左、右对应边，生成四面洞内壁。

默认 1×1×1 Cube、中心轴心、Z 两端面的示例：矩形角 X=±0.3、Y=±0.2，Z 各自保持在该面的 ±0.5，得到宽0.6、高0.4的洞。不能直接套用到尺寸不同或朝向不同的模型。

用户问过“洞里面透明”：模型是外壳，删面不自动生成内壁，普通单面材质背面也看不见。首先补四块内壁；已存在但朝反的单个面，Geometry → Flip Face Normals。不要用双面材质代替缺失几何，也不要盲目翻转整个物体。

用户后来明确说“好了”，截图出现桥接后的内壁。接手无需重新教一遍基础镂空。

固定墙体若仍有 BoxCollider，洞口会被挡住；可用正确引用修改后网格的非凸 MeshCollider，Convex 关闭。此建议针对固定场景墙体，不是任意动态刚体。

## 6. Cut 工具：已验证限制

- Cut 在已有面上定义新区域，产生可单独编辑的新面；不自动挖洞、不自动拆成两个物体、不自动贯穿背面。
- Cut Settings 的 Snap to existing edges and vertices 是吸到已有边、顶点。
- Snapping distance 控制吸附距离。
- 本地 CutTool.cs 确认：Ctrl 临时开启几何吸附，Shift 用于调整已放置的切割点。
- 没有“按住某键自动水平/垂直切割”的锁轴快捷键。
- Cut 没有使用 ProGrids 网格坐标取整的落点逻辑；不要声称打开 ProGrids 后 Cut 点击就自动落网格。
- 规则矩形可切完后，用顶点位置编辑器统一对应坐标。XY 面上左两点X相同、右两点X相同、上两点Y相同、下两点Y相同，Z保留面的位置。
- Cut 可能增加额外连接边，不能随意 Merge 周围所有面来消掉这些边，否则可能破坏开口拓扑。

## 7. 顶点位置编辑器与坐标

- 菜单：工具 → ProBuilder → Editors → Open Vertex Position Editor。
- Model Space 是模型自身坐标，World Space 是世界坐标。
- 窗口每行数字如109是实际共享顶点编号，不是教学中人为编号的第几组。
- 窗口空白并提示 Select a ProBuilder Mesh 时，需要选中模型的点/边/面等元素。
- 普通世界Y = 模型Y + Transform位置Y 的换算，仅在没有倾斜/缩放/父级复杂变换等前提下适用。
- 不能仅凭截图编造当前网格顶点编号；用真实坐标或实际网格数据核对。

## 8. ProGrids：已确认能用

- 版本3.0.3-preview.6。用户已打开左上角竖排工具栏，截图显示 .25 或 .5、ON。
- 开启：工具 → ProGrids → ProGrids Window。
- 竖栏顺序：1数字吸附间距；2网格显示；3 ON/OFF吸附开关；4箭头指向网格的一次吸附按钮；5锁定显示网格；下方X/Y/Z/3D网格平面。
- 第1个数字打开设置：Snap Value、Snap On Scale、Snap as Group、Angle、Predictive Grid。
- 正常开启后 W 拖动移动箭头即可吸附，不必按Ctrl。普通Cube也可用。
- 一次吸附按钮会取整X/Y/Z，可能改变高度；编辑模式还会通知ProBuilder对所选元素吸附。
- 顶部Unity原生横栏的0.25与左侧ProGrids数字不能混为同一设置，操作本包时查看竖栏数字。
- 显示网格关闭不等于吸附关闭。
- 编辑 → Preferences → ProGrids → Snap Method：Snap On Selected Axis 只对齐移动涉及的轴；Snap On All Axes 同时对齐三轴。
- 默认键：=间距翻倍；-减半；按住D在吸附开启时临时暂停；按住S临时切换轴模式；0重置网格倍率/偏移；[ ]调整锁定的显示网格；反斜杠切换视角。
- 按键在Scene获得焦点时使用，避免坐标输入框或右键飞行操作干扰。编辑 → 快捷方式…搜索ProGrids检查实际绑定。
- Snap as Group保持多选物体相对距离；Snap On Scale量化缩放值，不保证实际边缘自动贴合；Predictive Grid只切换显示参考平面；Angle只是辅助角度线，不是旋转吸附。
- 圆弧楼梯精确顶点不应被粗方格强制取整，否则会失去圆弧形状。

## 9. 早期任务：圆柱外侧旋转楼梯

创建：游戏对象 → ProBuilder → Curved Stair；或 Stairs 形状调 Circumference。

参数含义：Steps Generation为数量/每级高度；Steps Count台阶数；Circumference总转角，0直线、180半圈、360一圈、负值反向；Inner Radius控制中空；Sides生成内外侧墙和高端封口。

关键源码发现：弯曲楼梯先以min(SizeX,SizeZ)作原始外半径构造，再FitToSize到指定包围盒。乱设X/Z会把圆弧拉成椭圆，所以Inner Radius输入不必等于最终世界半径。Object Size(read only)来自Renderer世界轴对齐包围盒，旋转后与Shape Size不同。

早期截图：

- 默认Cylinder位置(-24.72,16.5,-23.44)，旋转0，Scale(10,8,10)：半径5、高16，底8.5、顶24.5（无父级变换前提）。
- 原楼梯位置(-21.80447,10.27778,-26.42119)，旋转Y=-210，Scale1；Shape Size(12,4,6)，18级，140度，InnerRadius4，Sides开。
- 助手按最终内半径5、外半径7、18级、140度计算：Shape Size(12.36231,4,6.98816)，InnerRadius4.99154；默认轴心和内部旋转下，Transform位置(-22.26382,10.27778,-26.05654)，旋转Y=-210。
- 这些为源码数学计算，未在Unity直接验证，不能当作任何后续截图的当前状态。
- 后续用户把楼梯位置改成(-26.61168,10.5,-26.4897)，旋转Y=-135。旧世界坐标不可继续直接用。

楼梯下方连续厚底板的手动方案：保留Sides，调整原来落地的内外圈底部点，每个角度位置的内外点同高，让侧墙下沿逐段上升，然后桥接每段内外底边补出连续底面。取消Sides只会留下踏面和立面薄壳，不会自动生成厚底板。

假定总高4、18级、中心轴心、原底点ModelY=-2：第i组底点目标Y=-2.2+i*(4/18)，i=0..18。这留出约0.2最小竖向厚度；入口点会比原底低0.2。要先确认原底点确实为-2，不可强套。

当位置Y=10.5且仅绕Y旋转、无其他变换时，世界Y=8.3+i*(4/18)。曾因用户窗口显示World而此前表用Model产生混淆；助手已纠正。不要把一切变形都简单归咎坐标模式，实际选点/编号也需验证。

用于核对默认模型的坐标公式（非当前顶点实测）：

- theta_i=i*140度/18；最终半径r内=5、r外=7。
- 包围盒中心偏移cx=-0.8188444491，cz=3.494078554。
- 模型X=-r*cos(theta_i)-cx，模型Z=r*sin(theta_i)-cz。
- 最高端外底点X≈6.1812、Z≈1.0054，内底点X≈4.6491、Z≈-0.2801，目标ModelY=1.8。
- 最低端外底点X≈-6.1812、Z≈-3.4941，内底点X≈-4.1812、Z≈-3.4941，目标ModelY=-2.2。
- 同一竖线顶面与底点X/Z相同，不能选错上方踏步顶点。

楼梯厚底方案是否全部做完未确认，用户后来转去学Cube、Cut和平台。不必自动回到楼梯任务。

## 10. 官方参考

以下文档可查，但快捷键可能残留旧说明，要与当前包实际注册和用户配置核对。

- ProBuilder 6.0.9 楼梯：https://docs.unity3d.com/Packages/com.unity.probuilder@6.0/manual/Stair.html
- Shape工具：https://docs.unity3d.com/Packages/com.unity.probuilder@6.0/manual/shape-tool.html
- Cut：https://docs.unity3d.com/Packages/com.unity.probuilder@6.0/manual/cut-tool.html
- 顶点坐标：https://docs.unity3d.com/Packages/com.unity.probuilder@6.0/manual/vertex-positions.html
- Bridge：https://docs.unity3d.com/Packages/com.unity.probuilder@6.0/manual/Edge_Bridge.html
- 删除面：https://docs.unity3d.com/Packages/com.unity.probuilder@6.0/manual/Face_Delete.html
- 合并面：https://docs.unity3d.com/Packages/com.unity.probuilder@6.0/manual/Face_Merge.html
- 焊接点：https://docs.unity3d.com/Packages/com.unity.probuilder@6.0/manual/Vert_Weld.html
- Inset：https://docs.unity3d.com/Packages/com.unity.probuilder@6.0/manual/Face_Inset.html
- 挤出：https://docs.unity3d.com/Packages/com.unity.probuilder@6.0/manual/Face_Extrude.html
- 转ProBuilder：https://docs.unity3d.com/Packages/com.unity.probuilder@6.0/manual/Object_ProBuilderize.html
- ProGrids界面：https://docs.unity3d.com/Packages/com.unity.progrids@3.0/manual/interface.html
- ProGrids快捷键：https://docs.unity3d.com/Packages/com.unity.progrids@3.0/manual/hotkeys.html
- Unity中文快捷键管理：https://docs.unity3d.com/cn/current/Manual/ShortcutsManager.html

## 11. 可直接发给新GPT的开场白

请先阅读这份交接文件。我使用中文版Unity 6.3 LTS、ProBuilder 6.0.9、ProGrids 3.0.3-preview.6，是新手，请用大白话解释，按当前版本给步骤，不要猜旧快捷键，也不要修改我的文件。我现在在做平台四周升墙，最新问题是Insert Edge Loop之后，截图里同一条横向轮廓被分成左短段、中长段、右短段，想知道怎么合并。请先结合我附上的最新截图，区分一起选择、消除分割、真正焊接，不要直接把所有点合并。
