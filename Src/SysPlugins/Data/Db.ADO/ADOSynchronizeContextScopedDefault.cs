﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Ccf.Ck.Libs.Logging;
using Ccf.Ck.Models.Enumerations;
using Ccf.Ck.SysPlugins.Interfaces;
using Ccf.Ck.SysPlugins.Iterators.DataNodes;
using Ccf.Ck.Utilities.Profiling;
using Ccf.Ck.Models.Settings;
using Ccf.Ck.SysPlugins.Interfaces.ContextualBasket;

namespace Ccf.Ck.SysPlugins.Data.Db.ADO
{
    public class ADOSynchronizeContextScopedDefault<XConnection> : IPluginsSynchronizeContextScoped, IADOTransactionScope, IContextualBasketConsumer
        where XConnection: DbConnection, new() {

        private DbConnection _DbConnection;
        private DbTransaction _DbTransaction;
        //private static Regex _DynamicParameterRegEx = new Regex(@"%(?<OnlyParameter>.+?)%", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static Regex _DynamicParameterRegEx = new Regex(@"%(\w+?)%", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public KraftGlobalConfigurationSettings KraftGlobalConfigurationSettings => ProcessingContext.InputModel.KraftGlobalConfigurationSettings;
        public IProcessingContext ProcessingContext { get; set; }
        public NodeExecutionContext.LoaderPluginContext LoaderContext { get; set; }

        #region IPluginsSynchronizeContextScoped
        public Dictionary<string, string> CustomSettings {
            get; set;
        }
        #endregion


        #region ITransactionScope
        public void CommitTransaction() {
            if (_DbTransaction != null) {
                _DbTransaction.Commit();
                _DbTransaction.Dispose();
                _DbTransaction = null;
            }
            if (_DbConnection != null) {
                _DbConnection.Close();
                _DbConnection.Dispose();
                _DbConnection = null;
                _DbTransaction = null;
            }
        }

        public void RollbackTransaction() {
            // We expect that the connection will dispose the transaction

            if (_DbTransaction != null) {
                _DbTransaction.Rollback();
                _DbTransaction.Dispose();
                _DbTransaction = null;
            }
            if (_DbConnection != null) {
                _DbConnection.Close();
                _DbConnection.Dispose();
                _DbConnection = null;
                _DbTransaction = null;
            }
        }

        public object StartTransaction() {
            if (_DbTransaction == null) {
                
                _DbTransaction = GetConnection().BeginTransaction();
            }
            return _DbTransaction;
        }
        public DbTransaction StartADOTransaction() {
            return StartTransaction() as DbTransaction;
        }
        #endregion

        #region IADOTransactionScope
        public virtual DbConnection Connection => GetConnection();

        public virtual DbTransaction CurrentTransaction => _DbTransaction;
        #endregion

        internal DbConnection GetConnection() {
            if (_DbConnection == null) {
                //Create connection
                string moduleRoot = System.IO.Path.Combine(KraftGlobalConfigurationSettings.GeneralSettings.ModulesRootFolder(ProcessingContext.InputModel.Module), ProcessingContext.InputModel.Module);
                // Support for @moduleroot@ variable replacement for connection strings that refer to file(s)
                string connectionString = (CustomSettings != null && CustomSettings.ContainsKey("ConnectionString"))
                         ? CustomSettings["ConnectionString"].Replace("@moduleroot@", moduleRoot)
                         : null;

                if (string.IsNullOrEmpty(connectionString)) {
                    throw new NullReferenceException("The Connection String must not be null or empty.");
                }

                // MatchCollection matches = _DynamicParameterRegEx.Matches(connectionString);
                connectionString = _DynamicParameterRegEx.Replace(connectionString, m => {
                    string varname = m.Groups[1].Value;
                    var val = LoaderContext.Evaluate(varname);
                    if (val.ValueType == EResolverValueType.Invalid) {
                        KraftLogger.LogError($"Expected parameter in connection string: {m.Groups[1].Value} was not resolved! Check that parameter's expression. It is recommended to not define it on node basis, but only in a nodeset root!");
                        // TODO: What shall we return on error? This is temporary decision - there should be something better or just excepton.
                        return m.Value;
                    }
                    if (!string.IsNullOrWhiteSpace(val.Value + "")) {
                        return val.Value.ToString();
                    } else {
                        KraftLogger.LogError($"Expected parameter in connection string: {m.Groups[1].Value} was not found or cannot be resolved!");
                        return m.Value;
                    }
                });

                /*if (matches.Count > 0) {
                    for (int i = 0; i < matches.Count; i++) {

                        string parameter = matches[i].Groups["OnlyParameter"].ToString();

                        if (ProcessingContext.InputModel.Data.ContainsKey(parameter)) {

                            connectionString = connectionString.Replace(matches[i].ToString(), ProcessingContext.InputModel.Data[parameter].ToString());
                        }
                        else if (ProcessingContext.InputModel.Client.ContainsKey(parameter)) {

                            connectionString = connectionString.Replace(matches[i].ToString(), ProcessingContext.InputModel.Client[parameter].ToString());
                        }
                        else {

                            KraftLogger.LogError($"Expected parameter in connection string: {matches[i]} was not found and the connection string remains invalid! Please consider the casing!");
                            //connectionString = connectionString.Replace(matches[i].ToString(), string.Empty);
                        }
                    }
                }*/

                _DbConnection = KraftProfiler.Current.ProfiledDbConnection(new XConnection());
                _DbConnection.ConnectionString = connectionString;
            }
            if (_DbConnection.State != ConnectionState.Open) {
                _DbConnection.Open();
            }
            return _DbConnection;
        }
        #region IContextualBasketConsumer
        public void InspectBasket(IContextualBasket basket) {
            var pc = basket.PickBasketItem<IProcessingContext>();
            ProcessingContext = pc;
            LoaderContext = basket.PickBasketItem<NodeExecutionContext.LoaderPluginContext>();
        }
        #endregion IContextualBasketConsumer
    }
}
