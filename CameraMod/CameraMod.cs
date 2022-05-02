using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using ZGenesis;
using ZGenesis.Attributes;
using ZGenesis.Events;
using ZGenesis.Mod;

namespace CameraMod {
    [GenesisMod]
    public class CameraMod : GenesisMod {
        public override string Name => "Camera Mod";
        public override string ModNamespace => "com.zephi.camera";
        public override string Description => "Mod that makes the ingame camera rotate by 90 degrees each rotation, rather than 180.";
        public override string Version => "v1.0.0";

        public CameraMod() {
            patchers.Add(new DependentPatcher(this, "playermotionfix", typeof(CameraMod)));
        }

        public override void PostPatches() {
            Patcher.RegisterEventHandler(new List<Type> { typeof(CameraRotateEvent) }, evt => {
                CameraRotateEvent e = (CameraRotateEvent) evt;
                if(e.Angle == -180f) {
                    e.CamController.cameraAngle += 90f;
                } else if(e.Angle == 180f) {
                    e.CamController.cameraAngle -= 90f;
                }
            });
        }



        private readonly static FieldInfo f_PlayerController__cam = typeof(PlayerController).GetField("cam", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly static MethodInfo m_Camera__get_transform = typeof(Camera).GetMethod("get_transform", BindingFlags.Public | BindingFlags.Instance);
        private readonly static MethodInfo m_Transform__get_forward = typeof(Transform).GetMethod("get_forward", BindingFlags.Public | BindingFlags.Instance);
        private readonly static MethodInfo m_Transform__get_right = typeof(Transform).GetMethod("get_right", BindingFlags.Public | BindingFlags.Instance);
        private readonly static MethodInfo m_Vector3__down = typeof(Vector3).GetMethod("get_down", BindingFlags.Public | BindingFlags.Static);
        private readonly static MethodInfo m_Vector3__Normalize = typeof(Vector3).GetMethod("Normalize", BindingFlags.Public | BindingFlags.Static);
        private readonly static MethodInfo m_Vector3__ProjectOnPlane = typeof(Vector3).GetMethod("ProjectOnPlane", BindingFlags.Public | BindingFlags.Static);
        private readonly static MethodInfo m_Vector3__multiply = typeof(Vector3).GetMethod("op_Multiply", BindingFlags.Public | BindingFlags.Static, null,
            new Type[] { typeof(Vector3), typeof(float) },
            new ParameterModifier[] { });
        private readonly static MethodInfo m_Vector3__addition = typeof(Vector3).GetMethod("op_Addition", BindingFlags.Public | BindingFlags.Static, null,
            new Type[] { typeof(Vector3), typeof(Vector3) },
            new ParameterModifier[] { });

        // Prevents VSCommunity from complaining about private methods.
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Reflection will call these when patched.")]
        [ModPatch("transpiler", "Assembly-CSharp", "PlayerController.MoveInput")]
        private static IEnumerable<CodeInstruction> PlayerInputMoveFixForCamera(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            generator.DeclareLocal(typeof(Transform));
            bool deletingArea = false;
            foreach(var instruction in instructions) {
                if(deletingArea && instruction.opcode == OpCodes.Br_S) deletingArea = false;
                if(!deletingArea) yield return instruction;
                if(instruction.opcode == OpCodes.Brfalse) {
                    deletingArea = true;
                    List<CodeInstruction> injection = new List<CodeInstruction>() {
                        // this.cam.transform
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, f_PlayerController__cam),
                        new CodeInstruction(OpCodes.Call, m_Camera__get_transform),
                        // Make & store a copy
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(OpCodes.Stloc_S, 6),
                        // Get Right direction
                        new CodeInstruction(OpCodes.Call, m_Transform__get_right),
                        new CodeInstruction(OpCodes.Call, m_Vector3__down),
                        new CodeInstruction(OpCodes.Call, m_Vector3__ProjectOnPlane),
                        new CodeInstruction(OpCodes.Call, m_Vector3__Normalize),
                        // Scale by x
                        new CodeInstruction(OpCodes.Ldloc_0),
                        new CodeInstruction(OpCodes.Call, m_Vector3__multiply),
                        // Get forward direction
                        new CodeInstruction(OpCodes.Ldloc_S, 6),
                        new CodeInstruction(OpCodes.Call, m_Transform__get_forward),
                        new CodeInstruction(OpCodes.Call, m_Vector3__down),
                        new CodeInstruction(OpCodes.Call, m_Vector3__ProjectOnPlane),
                        new CodeInstruction(OpCodes.Call, m_Vector3__Normalize),
                        // Scale by y
                        new CodeInstruction(OpCodes.Ldloc_1),
                        new CodeInstruction(OpCodes.Call, m_Vector3__multiply),
                        // Add together
                        new CodeInstruction(OpCodes.Call, m_Vector3__addition),
                        new CodeInstruction(OpCodes.Call, m_Vector3__Normalize),
                        // Store to vector2
                        new CodeInstruction(OpCodes.Stloc_2),
                    };
                    foreach(CodeInstruction instr in injection) {
                        yield return instr;
                    }
                }
            }
        }
    }
}
