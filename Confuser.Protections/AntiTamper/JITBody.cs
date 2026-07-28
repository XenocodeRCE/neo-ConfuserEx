using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using dnlib.IO;
using dnlib.PE;

namespace Confuser.Protections.AntiTamper {
	internal struct JITEHClause {
		public uint ClassTokenOrFilterOffset;
		public uint Flags;
		public uint HandlerLength;
		public uint HandlerOffset;
		public uint TryLength;
		public uint TryOffset;
	}

	internal class JITMethodBody : IChunk {
		public byte[] Body;
		public JITEHClause[] EHs;
		public byte[] ILCode;
		public byte[] LocalVars;
		public uint MaxStack;
		public uint MulSeed;

		public uint Offset;
		public uint Options;

		public FileOffset FileOffset { get; set; }

		public RVA RVA { get; set; }

		public void SetOffset(FileOffset offset, RVA rva) {
			this.FileOffset = offset;
			this.RVA = rva;
		}

		public uint GetFileLength() {
			return (uint)Body.Length + 4;
		}

		public uint GetVirtualSize() {
			return GetFileLength();
		}

		public uint CalculateAlignment() {
			return 0;
		}

		public void WriteTo(DataWriter writer) {
			writer.WriteUInt32((uint)(Body.Length >> 2));
			writer.WriteBytes(Body);
		}

		public void Serialize(uint token, uint key, byte[] fieldLayout) {
			using (var ms = new MemoryStream()) {
				var writer = new DataWriter(ms);
				foreach (byte i in fieldLayout)
					switch (i) {
						case 0:
							writer.WriteUInt32((uint)ILCode.Length);
							break;
						case 1:
							writer.WriteUInt32(MaxStack);
							break;
						case 2:
							writer.WriteUInt32((uint)EHs.Length);
							break;
						case 3:
							writer.WriteUInt32((uint)LocalVars.Length);
							break;
						case 4:
							writer.WriteUInt32(Options);
							break;
						case 5:
							writer.WriteUInt32(MulSeed);
							break;
					}

				writer.WriteBytes(ILCode);
				writer.WriteBytes(LocalVars);
				foreach (JITEHClause clause in EHs) {
					writer.WriteUInt32(clause.Flags);
					writer.WriteUInt32(clause.TryOffset);
					writer.WriteUInt32(clause.TryLength);
					writer.WriteUInt32(clause.HandlerOffset);
					writer.WriteUInt32(clause.HandlerLength);
					writer.WriteUInt32(clause.ClassTokenOrFilterOffset);
				}
				writer.WriteZeroes(4 - ((int)ms.Length & 3)); // pad to 4 bytes
				Body = ms.ToArray();
			}
			Debug.Assert(Body.Length % 4 == 0);
			// encrypt body
			uint state = token * key;
			uint counter = state;
			for (uint i = 0; i < Body.Length; i += 4) {
				uint data = Body[i] | (uint)(Body[i + 1] << 8) | (uint)(Body[i + 2] << 16) | (uint)(Body[i + 3] << 24);
				Body[i + 0] ^= (byte)(state >> 0);
				Body[i + 1] ^= (byte)(state >> 8);
				Body[i + 2] ^= (byte)(state >> 16);
				Body[i + 3] ^= (byte)(state >> 24);
				state += data ^ counter;
				counter ^= (state >> 5) | (state << 27);
			}
		}
	}

	internal class JITMethodBodyWriter : MethodBodyWriterBase {
		readonly CilBody body;
		readonly JITMethodBody jitBody;
		readonly bool keepMaxStack;
		readonly Metadata metadata;

		public JITMethodBodyWriter(Metadata md, CilBody body, JITMethodBody jitBody, uint mulSeed, bool keepMaxStack) :
			base(body.Instructions, body.ExceptionHandlers) {
			metadata = md;
			this.body = body;
			this.jitBody = jitBody;
			this.keepMaxStack = keepMaxStack;
			this.jitBody.MulSeed = mulSeed;
		}

		public void Write() {
			uint codeSize = InitializeInstructionOffsets();
			jitBody.MaxStack = keepMaxStack ? body.MaxStack : GetMaxStack();

			jitBody.Options = 0;
			if (body.InitLocals)
				jitBody.Options |= 0x10;

			if (body.Variables.Count > 0) {
				var local = new LocalSig(body.Variables.Select(var => var.Type).ToList());
				jitBody.LocalVars = SignatureWriter.Write(metadata, local);
			}
			else
				jitBody.LocalVars = new byte[0];

			var newCode = new byte[codeSize];
			var writer = new ArrayWriter(newCode);
			uint writtenCodeSize = WriteInstructions(ref writer);
			Debug.Assert(codeSize == writtenCodeSize);
			jitBody.ILCode = newCode;

			jitBody.EHs = new JITEHClause[exceptionHandlers.Count];
			if (exceptionHandlers.Count > 0) {
				jitBody.Options |= 8;
				for (int i = 0; i < exceptionHandlers.Count; i++) {
					ExceptionHandler eh = exceptionHandlers[i];
					jitBody.EHs[i].Flags = (uint)eh.HandlerType;

					uint tryStart = GetOffset(eh.TryStart);
					uint tryEnd = GetOffset(eh.TryEnd);
					jitBody.EHs[i].TryOffset = tryStart;
					jitBody.EHs[i].TryLength = tryEnd - tryStart;

					uint handlerStart = GetOffset(eh.HandlerStart);
					uint handlerEnd = GetOffset(eh.HandlerEnd);
					jitBody.EHs[i].HandlerOffset = handlerStart;
					jitBody.EHs[i].HandlerLength = handlerEnd - handlerStart;

					if (eh.HandlerType == ExceptionHandlerType.Catch) {
						uint token = metadata.GetToken(eh.CatchType).Raw;
						if ((token & 0xff000000) == 0x1b000000)
							jitBody.Options |= 0x80;

						jitBody.EHs[i].ClassTokenOrFilterOffset = token;
					}
					else if (eh.HandlerType == ExceptionHandlerType.Filter) {
						jitBody.EHs[i].ClassTokenOrFilterOffset = GetOffset(eh.FilterStart);
					}
				}
			}
		}

		protected override void WriteInlineField(ref ArrayWriter writer, Instruction instr) {
			writer.WriteUInt32(metadata.GetToken(instr.Operand).Raw);
		}

		protected override void WriteInlineMethod(ref ArrayWriter writer, Instruction instr) {
			writer.WriteUInt32(metadata.GetToken(instr.Operand).Raw);
		}

		protected override void WriteInlineSig(ref ArrayWriter writer, Instruction instr) {
			writer.WriteUInt32(metadata.GetToken(instr.Operand).Raw);
		}

		protected override void WriteInlineString(ref ArrayWriter writer, Instruction instr) {
			writer.WriteUInt32(metadata.GetToken(instr.Operand).Raw);
		}

		protected override void WriteInlineTok(ref ArrayWriter writer, Instruction instr) {
			writer.WriteUInt32(metadata.GetToken(instr.Operand).Raw);
		}

		protected override void WriteInlineType(ref ArrayWriter writer, Instruction instr) {
			writer.WriteUInt32(metadata.GetToken(instr.Operand).Raw);
		}
	}

	internal class JITBodyIndex : IChunk {
		readonly Dictionary<uint, JITMethodBody> bodies;

		public JITBodyIndex(IEnumerable<uint> tokens) {
			bodies = tokens.ToDictionary(token => token, token => (JITMethodBody)null);
		}

		public FileOffset FileOffset { get; set; }

		public RVA RVA { get; set; }

		public void SetOffset(FileOffset offset, RVA rva) {
			this.FileOffset = offset;
			this.RVA = rva;
		}

		public uint GetFileLength() {
			return (uint)bodies.Count * 8 + 4;
		}

		public uint GetVirtualSize() {
			return GetFileLength();
		}

		public uint CalculateAlignment() {
			return 0;
		}

		public void WriteTo(DataWriter writer) {
			uint length = GetFileLength() - 4; // minus length field
			writer.WriteUInt32((uint)bodies.Count);
			foreach (var entry in bodies.OrderBy(entry => entry.Key)) {
				writer.WriteUInt32(entry.Key);
				Debug.Assert(entry.Value != null);
				Debug.Assert((length + entry.Value.Offset) % 4 == 0);
				writer.WriteUInt32((length + entry.Value.Offset) >> 2);
			}
		}

		public void Add(uint token, JITMethodBody body) {
			Debug.Assert(bodies.ContainsKey(token));
			bodies[token] = body;
		}

		public void PopulateSection(PESection section) {
			uint offset = 0;
			foreach (var entry in bodies.OrderBy(entry => entry.Key)) {
				Debug.Assert(entry.Value != null);
				section.Add(entry.Value, 4);
				entry.Value.Offset = offset;

				Debug.Assert(entry.Value.GetFileLength() % 4 == 0);
				offset += entry.Value.GetFileLength();
			}
		}
	}
}
