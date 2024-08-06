using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
namespace JitMagic.MVVMLibLite {
	abstract public class OurViewModelBase : MVVMSObservableObject {
		protected Dictionary<Action, OurCommand> func_to_cmd;
		protected Dictionary<Func<Task>, OurCommand> func_to_cmd_tsk;


		protected OurCommand GetOurCmd(Func<Task> func, bool auto_disable = true) {
			OurCommand ret;
			if (func_to_cmd_tsk == null)
				func_to_cmd_tsk = new Dictionary<Func<Task>, OurCommand>();
			if (func_to_cmd_tsk.TryGetValue(func, out ret))
				return ret;
			return func_to_cmd_tsk[func] = new OurCommand(func, auto_disable);

		}

		protected OurCommand GetOurCmdSync(Action func, bool auto_disable = true, bool in_background = false) {
			OurCommand ret;
			if (func_to_cmd == null)
				func_to_cmd = new Dictionary<Action, OurCommand>();
			if (func_to_cmd.TryGetValue(func, out ret))
				return ret;
			return func_to_cmd[func] = new OurCommand(func, auto_disable, in_background);
		}
	}
}