using System;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Renamer;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Integrity
{
    internal class IntegrityInjectPhase : ProtectionPhase
    {
        public IntegrityInjectPhase(IntegrityProtection parent)
            : base(parent) { }

        public override ProtectionTargets Targets => ProtectionTargets.Modules;

        public override string Name => "Integrity runtime injection";

        protected override void Execute(ConfuserContext context, ProtectionParameters parameters)
        {
            var module = context.CurrentModule;

            // Load signer (fails early if env var is missing)
            IBuildSigningService signer;
            try
            {
                signer = new RsaBuildSigningService();
            }
            catch (Exception ex)
            {
                context.Logger.Error("Integrity protection: " + ex.Message);
                throw;
            }

            context.Logger.Info("Integrity: public key fingerprint " + signer.KeyFingerprint);

            var ctx = new IntegrityContext
            {
                Context = context,
                Module = module,
                Protection = (IntegrityProtection)Parent,
                PublicKey = signer.GetPublicKey()
            };

            context.Annotations.Set(module, IntegrityProtection.ContextKey, ctx);
            context.Annotations.Set(module, IntegrityContext.SignerKey, signer);

            // Inject runtime
            var rt = context.Registry.GetService<IRuntimeService>();
            var name = context.Registry.GetService<INameService>();
            var marker = context.Registry.GetService<IMarkerService>();

            try
            {
                IntegrityRuntimeInjector.Inject(context, (IntegrityProtection)Parent, ctx, rt, name, marker);
            }
            catch (Exception ex)
            {
                context.Logger.Warn("Integrity runtime injection failed, skipping: " + ex.Message);
                context.Logger.Debug(ex.ToString());
                // Clean up annotations to prevent post-processing errors
                context.Annotations.Set(module, IntegrityProtection.ContextKey, (IntegrityContext)null);
                context.Annotations.Set(module, IntegrityContext.SignerKey, (IBuildSigningService)null);
                return;
            }

            // Read checkMode
            string checkMode = parameters.GetParameter(
                context, module, "checkMode", "manual");

            if (checkMode == "startup")
            {
                // Call Verify() from .cctor, store status in a static field
                var cctor = module.GlobalType.FindStaticConstructor();
                cctor.Body.Instructions.Insert(0,
                    Instruction.Create(OpCodes.Call, ctx.VerifyMethod));
                // Pop the int return value (IntegrityStatus)
                cctor.Body.Instructions.Insert(1,
                    Instruction.Create(OpCodes.Pop));
            }
            // checkMode=manual: the Verify method is public/internal, app calls it manually
        }
    }
}
