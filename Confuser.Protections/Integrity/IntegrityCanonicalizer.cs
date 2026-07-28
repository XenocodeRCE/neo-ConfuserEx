using System;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Integrity
{
    internal static class IntegrityCanonicalizer
    {
        /// <summary>Read raw bytes from an EmbeddedResource.</summary>
        public static byte[] CanonicalizeEmbedded(EmbeddedResource r)
        {
            if (r == null || r.Data == null)
                return new byte[0];
            try
            {
                var data = r.Data;
                data.Position = 0;
                var result = new byte[data.Length];
                data.Read(result, 0, result.Length);
                return result;
            }
            catch
            {
                return new byte[0];
            }
        }

        /// <summary>
        ///     Produces a stable CIL digest for build reports.
        ///     NOT used in runtime verification.
        ///     Format: opcode name, binary operands, branch targets by index,
        ///     full-name references, EH bounds.
        /// </summary>
        public static byte[] BuildReportCilDigest(MethodDef method)
        {
            if (method == null || !method.HasBody)
                return new byte[0];

            var body = method.Body;
            var sb = new StringBuilder();

            // Build instruction index map
            var indexMap = new System.Collections.Generic.Dictionary<Instruction, int>();
            for (int i = 0; i < body.Instructions.Count; i++)
                indexMap[body.Instructions[i]] = i;

            foreach (var instr in body.Instructions)
            {
                sb.Append(instr.OpCode.Name);

                if (instr.Operand is string s)
                    sb.Append(':').Append(s);
                else if (instr.Operand is int i32)
                    sb.Append(':').Append(i32);
                else if (instr.Operand is long i64)
                    sb.Append(':').Append(i64);
                else if (instr.Operand is float f)
                    sb.Append(':').Append(f.ToString("G9"));
                else if (instr.Operand is double d)
                    sb.Append(':').Append(d.ToString("G17"));
                else if (instr.Operand is Instruction target && indexMap.TryGetValue(target, out int targetIdx))
                    sb.Append("->").Append(targetIdx);
                else if (instr.Operand is Instruction[] targets)
                    foreach (var t in targets)
                        if (indexMap.TryGetValue(t, out int ti))
                            sb.Append("->").Append(ti);
                else if (instr.Operand is IMemberRef mr)
                    sb.Append(':').Append(mr.FullName);
                else if (instr.Operand is Local local)
                    sb.Append(":loc").Append(local.Index);
                else if (instr.Operand is Parameter param)
                    sb.Append(":arg").Append(param.Index);
                else if (instr.Operand != null)
                    sb.Append(':').Append(instr.Operand);

                sb.Append('|');
            }

            // EH bounds
            foreach (var eh in body.ExceptionHandlers)
            {
                sb.Append("EH:").Append(eh.HandlerType).Append('|');
                if (eh.TryStart != null) sb.Append("TS:").Append(indexMap[eh.TryStart]).Append('|');
                if (eh.TryEnd != null) sb.Append("TE:").Append(indexMap[eh.TryEnd]).Append('|');
                if (eh.HandlerStart != null) sb.Append("HS:").Append(indexMap[eh.HandlerStart]).Append('|');
                if (eh.HandlerEnd != null) sb.Append("HE:").Append(indexMap[eh.HandlerEnd]).Append('|');
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}

