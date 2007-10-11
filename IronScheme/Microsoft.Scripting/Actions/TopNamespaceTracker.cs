/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Microsoft Permissive License. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Microsoft Permissive License, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Microsoft Permissive License.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Reflection;

using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Utils;
using Microsoft.Scripting.Actions;

namespace Microsoft.Scripting.Actions {
    /// <summary>
    /// Represents the top reflected package which contains extra information such as
    /// all the assemblies loaded and the built-in modules.
    /// </summary>
    public class TopNamespaceTracker : NamespaceTracker {
        private int _initialized;
        private bool _isolated;
        private int _lastDiscovery = 0;

        internal TopNamespaceTracker()
            : base(null) {
            SetTopPackage(this);
        }

        /// <summary>
        /// Creates a top reflected package that is optionally isolated
        /// from all other packages in the system.
        /// </summary>
        public TopNamespaceTracker(bool isolated)
            : this() {
            this._isolated = isolated;
        }

        #region Public API Surface

        /// <summary>
        /// returns the package associated with the specified namespace and
        /// updates the associated module to mark the package as imported.
        /// </summary>
        public NamespaceTracker TryGetPackage(string name) {
            return TryGetPackage(SymbolTable.StringToId(name));
        }

        public NamespaceTracker TryGetPackage(SymbolId name) {
            NamespaceTracker pm = TryGetPackageAny(name) as NamespaceTracker;
            if (pm != null) {
                return pm;
            }
            return null;
        }

        public MemberTracker TryGetPackageAny(string name) {
            return TryGetPackageAny(SymbolTable.StringToId(name));
        }

        public MemberTracker TryGetPackageAny(SymbolId name) {
            Initialize();
            MemberTracker ret;
            if (TryGetValue(name, out ret)) {
                return ret;
            }
            return null;
        }

        public MemberTracker TryGetPackageLazy(SymbolId name) {
            MemberTracker ret;
            if (_dict.TryGetValue(SymbolTable.IdToString(name), out ret)) {
                return ret;
            }
            return null;
        }

        /// <summary>
        /// Ensures that the assembly is loaded
        /// </summary>
        /// <param name="assem"></param>
        /// <returns>true if the assembly was loaded for the first time. 
        /// false if the assembly had already been loaded before</returns>
        public bool LoadAssembly(Assembly assem) {
            lock (this) {
                if (_packageAssemblies.Contains(assem)) {
                    // The assembly is already loaded. There is nothing more to do
                    return false;
                }

                _packageAssemblies.Add(assem);
                UpdateId();
            }

            EventHandler<AssemblyLoadedEventArgs> assmLoaded = AssemblyLoaded;
            if (assmLoaded != null) {
                assmLoaded(this, new AssemblyLoadedEventArgs(assem));
            }
            return true;
        }


        public void Initialize() {
            if (_initialized != 0) return;
            if (System.Threading.Interlocked.Exchange(ref _initialized, 1) == 0) {

                // add mscorlib
                ClrModule.GetInstance().AddReference(typeof(string).Assembly);
                // add system.dll
                ClrModule.GetInstance().AddReference(typeof(System.Diagnostics.Debug).Assembly);
            }
        }

        #endregion

        protected override void LoadNamespaces() {
            lock (this) {
                for (int i = _lastDiscovery; i < PackageAssemblies.Count; i++) {
                    DiscoverAllTypes(PackageAssemblies[i]);
                }
                _lastDiscovery = PackageAssemblies.Count;
            }
        }

        public event EventHandler<AssemblyLoadedEventArgs> AssemblyLoaded;
    }
}
