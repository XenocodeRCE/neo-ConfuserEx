using System;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace Confuser.Core {
	/// <summary>
	///     The listener of module writer event.
	/// </summary>
	public class ModuleWriterListener {
		/// <summary>
		///     Attaches this compatibility listener to dnlib's current writer-event API.
		/// </summary>
		public void Attach(ModuleWriterOptionsBase options) {
			if (options == null)
				throw new ArgumentNullException(nameof(options));
			options.WriterEvent += HandleWriterEvent;
		}

		void HandleWriterEvent(object sender, ModuleWriterEventArgs args) {
			if (args.Event == ModuleWriterEvent.PESectionsCreated)
				NativeEraser.Erase(args.Writer as NativeModuleWriter, args.Writer.Module as ModuleDefMD);
			OnWriterEvent?.Invoke(args.Writer, new ModuleWriterListenerEventArgs(args.Event));
		}

		/// <summary>
		///     Occurs when a module writer event is triggered.
		/// </summary>
		public event EventHandler<ModuleWriterListenerEventArgs> OnWriterEvent;
	}

	/// <summary>
	///     Indicates the triggered writer event.
	/// </summary>
	public class ModuleWriterListenerEventArgs : EventArgs {
		/// <summary>
		///     Initializes a new instance of the <see cref="ModuleWriterListenerEventArgs" /> class.
		/// </summary>
		/// <param name="evt">The triggered writer event.</param>
		public ModuleWriterListenerEventArgs(ModuleWriterEvent evt) {
			WriterEvent = evt;
		}

		/// <summary>
		///     Gets the triggered writer event.
		/// </summary>
		/// <value>The triggered writer event.</value>
		public ModuleWriterEvent WriterEvent { get; private set; }
	}
}
