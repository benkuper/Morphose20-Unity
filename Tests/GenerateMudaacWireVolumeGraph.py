"""Regenerate the HDRP graph from the project's existing Unlit graph. Import the HLSL first."""
import copy
import json
import re
import uuid
from pathlib import Path


def read_graph(path):
    source = Path(path).read_text(encoding="utf-8")
    decoder = json.JSONDecoder()
    result, pos = [], 0
    while pos < len(source):
        while pos < len(source) and source[pos].isspace():
            pos += 1
        if pos == len(source):
            break
        item, pos = decoder.raw_decode(source, pos)
        result.append(item)
    return result


def uid():
    return uuid.uuid4().hex


def ref(item):
    return {"m_Id": item["m_ObjectId"]}


source = read_graph("Assets/Shaders/UnlitShadowReceiver.shadergraph")
lookup = {item["m_ObjectId"]: item for item in source}
graph = source[0]
blocks = [item for item in source if item["m_Type"].endswith(".BlockNode")
          and item["m_Name"] != "SurfaceDescription.ShadowTint"]
kept = {graph["m_ObjectId"]}
for block in blocks:
    kept.add(block["m_ObjectId"])
    kept.update(slot["m_Id"] for slot in block["m_Slots"])


def include(item):
    if item["m_ObjectId"] in kept:
        return
    kept.add(item["m_ObjectId"])
    for value in item.values():
        for entry in value if isinstance(value, list) else [value]:
            if isinstance(entry, dict) and entry.get("m_Id") in lookup:
                include(lookup[entry["m_Id"]])


for target in graph["m_ActiveTargets"]:
    include(lookup[target["m_Id"]])
output = [item for item in source if item["m_ObjectId"] in kept]
graph["m_Nodes"] = [ref(block) for block in blocks]
for key in ("m_Properties", "m_CategoryData", "m_Keywords", "m_Edges", "m_GroupDatas", "m_StickyNoteDatas", "m_SubDatas"):
    graph[key] = []
for context in ("m_VertexContext", "m_FragmentContext"):
    graph[context]["m_Blocks"] = [r for r in graph[context]["m_Blocks"] if r["m_Id"] in kept]
for item in output:
    if item["m_Type"].endswith(".SystemData"):
        item.update(m_SurfaceType=1, m_RenderingPass=4, m_ZWrite=False, m_MaterialNeedsUpdateHash=0)
    if item["m_Type"].endswith(".HDUnlitData"):
        item["m_EnableShadowMatte"] = False
    if item["m_Type"].endswith(".BuiltinData"):
        item["m_TransparencyFog"] = False

property_template = next(item for item in source if item["m_Type"].endswith(".ColorShaderProperty"))
node_template = next(item for item in source if item["m_Type"].endswith(".PropertyNode"))
slot_template = next(item for item in source if item["m_Type"].endswith(".Vector4MaterialSlot"))
category = {"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.CategoryData", "m_ObjectId": uid(),
            "m_Name": "Wire Volume (driven by modifier)", "m_ChildObjectList": []}
output.append(category)
graph["m_CategoryData"] = [ref(category)]


def node(kind, name):
    item = copy.deepcopy(node_template)
    item.update(m_Type="UnityEditor.ShaderGraph." + kind, m_ObjectId=uid(), m_Name=name, m_Slots=[], m_Precision=1)
    item.pop("m_Property", None)
    output.append(item)
    graph["m_Nodes"].append(ref(item))
    return item


def slot(owner, index, name, dimension=4, direction=0):
    item = copy.deepcopy(slot_template)
    item.update(m_Type="UnityEditor.ShaderGraph.Vector%dMaterialSlot" % dimension, m_ObjectId=uid(), m_Id=index,
                m_DisplayName=name, m_ShaderOutputName=name, m_SlotType=direction)
    item["m_Value"] = item["m_DefaultValue"] = 0.0 if dimension == 1 else dict.fromkeys("xyzw"[:dimension], 0.0)
    output.append(item)
    owner["m_Slots"].append(ref(item))


def edge(src, src_slot, dest, dest_slot):
    graph["m_Edges"].append({"m_OutputSlot": {"m_Node": ref(src), "m_SlotId": src_slot},
                             "m_InputSlot": {"m_Node": ref(dest), "m_SlotId": dest_slot}})


function = node("CustomFunctionNode", "MudaacWireVolume (Custom Function)")
function.update(m_SGVersion=1, m_SourceType=0, m_FunctionName="MudaacWireVolume", m_FunctionBody="",
                m_FunctionSource=re.search(r"guid: (\w+)", Path("Assets/Shaders/MudaacWireVolume.hlsl.meta").read_text()).group(1),
                m_FunctionSourceUsePragmas=True)
for index, kind, name in ((0, "PositionNode", "Position"), (1, "ViewDirectionNode", "ViewDirection")):
    geometry = node(kind, name)
    geometry.update(m_SGVersion=1, m_Space=0)
    if index == 0:
        geometry["m_PositionSource"] = 0
    slot(geometry, 0, "Out", 3, 1)
    slot(function, index, name, 3)
    edge(geometry, 0, function, index)

inputs = [
    ("BaseColor", "_BaseColor", (1, 1, 1, 1), True),
    ("EdgeColor", "_WireColor", (.75, .75, .75, 1), True),
    ("FogColor", "_WireFogColor", (.35, .35, .35, 1), True),
    ("Center", "_WireCenter", (0, 0, 0, 0), False),
    ("HalfSize", "_WireHalfSize", (.5, .5, .5, 1), False),
    ("Edge", "_WireEdge", (.006, .02, .12, .35), False),
    ("Surface", "_WireSurface", (.025, .75, 1, 1), False),
    ("Volume", "_WireVolume", (.15, 3, 4, 6), False),
    ("Texture", "_WireTexture", (2.5, .65, 140, 7), False),
    ("Motion", "_WireMotion", (0, 0, 0, .65), False),
]
for index, (name, reference, value, color) in enumerate(inputs, 2):
    prop = copy.deepcopy(property_template)
    prop.update(m_ObjectId=uid(), m_Guid={"m_GuidSerialized": str(uuid.uuid4())}, m_Name=name,
                m_DefaultReferenceName=reference, m_OverrideReferenceName=reference,
                m_RefNameGeneratedByDisplayName=name, isMainColor=name == "BaseColor")
    prop["m_Value"] = dict(zip("rgba" if color else "xyzw", value))
    if color:
        prop["m_ColorMode"] = 1
    else:
        prop["m_Type"] = "UnityEditor.ShaderGraph.Internal.Vector4ShaderProperty"
        prop.pop("isMainColor", None)
        prop.pop("m_ColorMode", None)
    output.append(prop)
    graph["m_Properties"].append(ref(prop))
    category["m_ChildObjectList"].append(ref(prop))
    pn = node("PropertyNode", name)
    pn["m_Property"] = ref(prop)
    pn["m_DrawState"]["m_Position"].update(x=-450, y=index * 150, width=180, height=130)
    slot(pn, 0, "Out", 4, 1)
    slot(function, index, name)
    edge(pn, 0, function, index)
for index, (name, dimension, block_name) in enumerate([
    ("Color", 3, "BaseColor"), ("Alpha", 1, "Alpha")], len(inputs) + 2):
    slot(function, index, name, dimension, 1)
    block = next(item for item in blocks if item["m_Name"] == "SurfaceDescription." + block_name)
    edge(function, index, block, 0)
serialized = "\n\n".join(json.dumps(item, indent=4) for item in output) + "\n"
for item in output:
    serialized = serialized.replace(item["m_ObjectId"], uid())
Path("Assets/Shaders/MudaacWireVolume.shadergraph").write_text(serialized, encoding="utf-8")
print("Created HDRP Unlit wire volume graph:", len(output), "serialized objects")
