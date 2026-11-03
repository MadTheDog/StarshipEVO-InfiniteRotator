using System;
using System.IO;
using System.Text;
using Game;
using MessagePack;
using UI;
using VoxelEngine;

namespace InfiniteRotator
{
    public struct SpinSettings
    {
        [CommandCustomizableField("Run continuously", null)]
        public bool Run { get; set; }

        [CommandCustomizableFieldRange("Speed (deg/s, before load slowdown)", 0f, 180f, "F1", null)]
        public float Speed { get; set; }

        [CommandCustomizableField("Reverse direction", null)]
        [CommandTooltip("Changes rotation direction. The current angle stays unchanged.")]
        public bool Reverse { get; set; }
    }

    public sealed class InfiniteRotatorSystem : RotatorSystem, ICommandOnBeforeSaveCallback
    {
        private SpinSettings settings = new SpinSettings { Speed = 45f };
        private bool loading;
        private bool pendingResume;
        private bool rejectedSave;

        public override string OverTextIndirect => brickCoord.GetTagLabel("Infinite Rotator");
        public override bool CanBeLinkSource => false;

        public override bool CanBeLinkTargetFromCommand(ICommand predecessor)
            => !(predecessor is ComputerTurret) && !(predecessor is MechComputer)
                && !(predecessor is CommandSequencerController) && !(predecessor is CommandHandle)
                && base.CanBeLinkTargetFromCommand(predecessor);

        protected override void OnInitialize(BrickIds ids, bool onLoad, params object[] args)
        {
            base.OnInitialize(ids, onLoad, args);
            SetNativeSpeed();
            if (!onLoad) brickCoord.Parent.BrickCount.SetDefaultLabel(brickCoord, "Infinite Rotator");
        }

        protected override void InitChildrenTransform()
        {
            base.InitChildrenTransform();

            // Native placement adds the top before this hook. Native loading
            // registers saved child bricks before reconnecting their controller.
            var child = ChildrenEntity;
            var bricks = child.Bricks;
            var top = bricks.GetBrickData(1);
            if (top.brickId == TabMechanism.ROTATOR_TOP.brickID)
            {
                // Upgrade the existing child in place, preserving its instance ID,
                // attached construction and mechanism storage connection.
                var coord = new BrickCoord { entityID = child.ID, brickData = top };
                child.Parent.BrickCount.SetCountForBrickId(coord, -1);
                top.brickId = Plugin.TopId;
                coord.brickData = top;
                child.Parent.BrickCount.SetCountForBrickId(coord, 1);

                var ids = bricks.GetBrickIds(top.instanceId);
                Brick.ReleaseRenderData(GlobalRenderer.renderer, ids);
                ids.renderId1 = ids.renderId2 = ids.renderId3 = 0;
                top.color = InfiniteRotorTop.White;
                top.Brick.RegisterRenderData(top, GlobalRenderer.renderer, child.transformId, ref ids);
                bricks.UpdateIds(ids, top.instanceId);
            }
            else if (top.brickId != Plugin.TopId || top.color.Equals(InfiniteRotorTop.White)) return;

            top.color = InfiniteRotorTop.White;
            bricks.Update(top);
        }

        private void SetNativeSpeed()
        {
            var native = saveData;
            native.velocity = 1f;
            native.isReversed = false; // Direction belongs to signed motion; toggling the native mapping would jump the pose.
            saveData = native;
        }

        public override void Activate(bool manualActivation, PlayerCommandManager playerManager, float state)
        {
            if (manualActivation && playerManager != null && playerManager.isLocalPlayer)
            {
                lastPlayerManager = playerManager;
                OpenSettings();
                return;
            }
            if (!SpinPolicy.Finite(state) || state < 0 || IsRemoved) return;
            settings.Run = state > .5f;
            Propagate();
            parentEntity?.SetDirty();
        }

        private void OpenSettings()
        {
            UI_CommandGeneric.OpenConfig(new UI_CommandGeneric.WindowsParams
            {
                command = this, title = "Infinite Rotator (draft)", commandData = settings,
                brickCoordForLabel = brickCoord
            });
        }

        protected override void OnGenericParamsUpdated(object commandData, bool onClose)
        {
            settings = (SpinSettings)commandData;
            settings.Speed = SpinPolicy.SpeedOrDefault(settings.Speed);
            if (settings.Speed == 0 || rejectedSave) settings.Run = false;
            Propagate();
            if (!onClose) OpenSettings();
        }

        protected override void Propagate()
        {
            if (loading || ChildrenEntity == null || parentEntity == null || IsRemoved) return;
            pendingResume = false;
            if (rejectedSave || !settings.Run || settings.Speed <= 0 || parentEntity.Systems.IsDestroyed)
            {
                settings.Run = false;
                NativeMovement.Stop(this);
                State = tmpState;
                return;
            }
            State = tmpState + (settings.Reverse ? -1f : 1f);
            base.Propagate(); // Allocates the native collision query, starts sound, and joins the native movement list.
        }

        protected override float GetMoveSpeed() => base.GetMoveSpeed() * settings.Speed / 180f;

        internal bool BeforeNativeUpdate()
        {
            if (!settings.Run || parentEntity == null || parentEntity.Systems.IsDestroyed)
            {
                settings.Run = false;
                NativeMovement.Stop(this);
                return false;
            }
            State = tmpState + (settings.Reverse ? -1f : 1f);
            return NativeMovement.IsRunning(this);
        }

        internal void AfterNativeUpdate()
        {
            if (IsRemoved || ChildrenEntity == null) return;
            float phase = tmpState, rollback = NativeMovement.Previous(this);
            SpinPolicy.Rebase(ref phase, ref rollback);
            NativeMovement.SetPrevious(this, rollback);
            ForceState(phase); // Updates angle/job state as well as the managed phase. Pose is unchanged by whole turns.
            StateDataPointer.Set(phase);
            if (!NativeMovement.IsRunning(this) && settings.Run)
            {
                settings.Run = false; // Collision/overload stop is latched until the user starts again.
                parentEntity?.SetDirty();
            }
        }

        public void OnBeforeSave() => State = SpinPolicy.Normalize(tmpState);

        public override byte[] GetCustomData(bool isBlueprintExport)
            => SpinPolicy.Encode(base.GetCustomData(isBlueprintExport), settings.Run, settings.Reverse, settings.Speed, tmpState);

        public override void OnSaveLoaded(byte[] customData, bool onInit)
        {
            loading = true;
            NativeMovement.Stop(this);
            try
            {
                var saved = SpinPolicy.Decode(customData);
                ValidateNativeSave(saved.Native);
                settings = new SpinSettings { Run = saved.Run, Reverse = saved.Reverse, Speed = saved.Speed };
                State = saved.Phase;
                tmpState = saved.Phase;
                base.OnSaveLoaded(saved.Native, onInit);
                if (IsRemoved || ChildrenEntity == null)
                {
                    settings.Run = false;
                    pendingResume = false;
                    return; // Native child validation can delete this block and release its state handles.
                }
                rejectedSave = false;
                SetNativeSpeed();
                SetStateFromShipyard(saved.Phase); // Reapply after native child initialization using the saved phase.
                StateDataPointer.Set(saved.Phase);
                pendingResume = settings.Run;
            }
            catch (InvalidDataException error)
            {
                settings = new SpinSettings { Speed = 45f };
                pendingResume = false;
                rejectedSave = true;
                State = tmpState = 0;
                Plugin.Log.LogError("Infinite Rotator save rejected; movement disabled: " + error.Message);
                // Do not feed unknown custom bytes to the native MessagePack reader.
                base.OnSaveLoaded(null, onInit);
            }
            finally { loading = false; }
            if (!onInit && pendingResume) Propagate();
        }

        private static void ValidateNativeSave(byte[] bytes)
        {
            RotatorSaveData native;
            try { native = MessagePackSerializer.Deserialize<RotatorSaveData>(bytes); }
            catch (Exception error) { throw new InvalidDataException("Invalid native rotator data.", error); }
            // Only the native version emitted by this draft is accepted. The base loader otherwise swallows errors.
            if (native.version != 2 || !SpinPolicy.Finite(native.velocity) || native.velocity != 1f || native.isReversed
                || !SpinPolicy.Finite(native.minimumAngle) || !SpinPolicy.Finite(native.maximumAngle)
                || !SpinPolicy.Finite(native.restingPosition) || !SpinPolicy.Finite(native.axisOffsetLegacy.x)
                || !SpinPolicy.Finite(native.axisOffsetLegacy.y) || !SpinPolicy.Finite(native.axisOffsetLegacy.z))
                throw new InvalidDataException("Unsupported native rotator settings/version.");
        }

        public override void OnLoadingSequenceFinished()
        {
            base.OnLoadingSequenceFinished();
            if (pendingResume) Propagate();
        }

        public override void GetSidePanelInfoText(StringBuilder text)
        {
            base.GetSidePanelInfoText(text);
            text.Append("\nInfinite rotation: ").Append(settings.Run ? "running" : "stopped")
                .Append(settings.Reverse ? " / reverse" : " / forward")
                .Append("\nRequested speed: ").Append(settings.Speed.ToString("F1")).Append(" deg/s");
            if (rejectedSave) text.Append("\nSave rejected: rebuild this block before use.");
        }
    }
}
