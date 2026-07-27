using Confuser.Core;

namespace Confuser.Protections.Integrity
{
    [BeforeProtection("Ki.AntiTamper")]
    internal sealed class IntegrityProtection : Protection
    {
        public const string _Id = "integrity";
        public const string _FullId = "Ki.Integrity";
        internal static readonly object ContextKey = new object();

        public override string Name
        {
            get { return "Signed integrity manifest"; }
        }

        public override string Description
        {
            get { return "Verifies signed digests of selected resources."; }
        }

        public override string Id { get { return _Id; } }
        public override string FullId { get { return _FullId; } }

        public override ProtectionPreset Preset
        {
            get { return ProtectionPreset.Maximum; }
        }

        protected override void Initialize(ConfuserContext context) { }

        protected override void PopulatePipeline(ProtectionPipeline pipeline)
        {
            pipeline.InsertPreStage(
                PipelineStage.OptimizeMethods,
                new IntegrityInjectPhase(this));

            pipeline.InsertPreStage(
                PipelineStage.EndModule,
                new IntegrityWriterPhase(this));
        }
    }
}
