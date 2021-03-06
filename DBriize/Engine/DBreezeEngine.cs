﻿/* 
  Copyright (C) 2012 DBriize.tiesky.com / Alex Solovyov / Ivars Sudmalis.
  It's a free software for those, who think that it should be free.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

using DBriize.Transactions;
using DBriize.Exceptions;
using DBriize.TextSearch;

//under DBriize main namespace we hold Schema and Engine.

namespace DBriize
{
    /// <summary>
    /// Main DBriize Database class.
    /// </summary>
    public class DBriizeEngine:IDisposable
    {
        #region "Version Number"
        /// <summary>
        /// DBriize version number
        /// </summary>        
        //public static string Version = "01.061.20131120";
        //public static string Version = "01.068.20141205";
        //public static string Version = "01.072.20150522"; //Get it from assembly
        #endregion

   
        //later can be swapped on Configuration.DBriizeDataFolderName;
        internal string MainFolder = String.Empty;
        internal Scheme DBriizeSchema = null;
        internal TextDeferredIndexer DeferredIndexer = null;
        internal TransactionsCoordinator _transactionsCoordinator = null;
        //internal bool DBisOperable = true;
        /// <summary>
        /// Db is not operable any more by DBisOperableReason reason 
        /// </summary>
        public bool DBisOperable { get; internal set; } = true;
        /// <summary>
        /// Is filled with a text note who brought to DBisOperable = false
        /// </summary>
        public string DBisOperableReason { get; internal set; } = String.Empty;        
        internal TransactionsJournal _transactionsJournal = null;
        internal TransactionTablesLocker _transactionTablesLocker = null;
        /// <summary>
        /// Whether engine is disposed
        /// </summary>
        public bool Disposed { get { return disposed == 1; }}
        int disposed = 0;

        /// <summary>
        /// Initialized from DBriizeRemoteEngine
        /// </summary>
        internal bool RemoteEngine = false;
        /// <summary>
        /// DBriize may execute some tasks in the background (like deffered text indexing). 
        /// External delegate can receive notifications about that.
        /// </summary>
        public Action<string, object> BackgroundTasksExternalNotifier = null;
        /// <summary>
        /// DBriize Configuration.
        /// For now BackupPlan is included.
        /// Later can be added special settings for each entity defined by string pattern.
        /// </summary>
        internal DBriizeConfiguration Configuration = new DBriizeConfiguration();

        /// <summary>
        /// For DBriizeRemoteEngine wrapper
        /// </summary>
        internal DBriizeEngine() { }

        /// <summary>
        /// DBriize instantiator
        /// </summary>
        /// <param name="DBriizeConfiguration"></param>
        public DBriizeEngine(DBriizeConfiguration DBriizeConfiguration)
        {
            ConstructFromConfiguration(DBriizeConfiguration);

            //if (Configuration != null)
            //    Configuration = DBriizeConfiguration;
            
            ////Setting up in backup DBriizeFolderName, there must be found at least TransJournal and Scheme.
            ////Configuration.Backup.SynchronizeBackup has more information
            //if (Configuration.Backup.IsActive)
            //{
            //    Configuration.Backup.DBriizeFolderName = Configuration.DBriizeDataFolderName;

            //    ////Running backup synchronization
            //    //Configuration.Backup.SynchronizeBackup();
            //}

            //MainFolder = Configuration.DBriizeDataFolderName;

            //InitDb();

            ////Console.WriteLine("DBriize notification: Don't forget in the dispose function of your DLL or main application thread");
            ////Console.WriteLine("                      to dispose DBriize engine:  if(_engine != null) _engine.Dispose(); ");
            ////Console.WriteLine("                      to get graceful finilization of all working threads! ");
        }

        /// <summary>
        /// Constructing DBriize from DBriizeConfiguration
        /// </summary>
        /// <param name="DBriizeConfiguration"></param>
        internal void ConstructFromConfiguration(DBriizeConfiguration DBriizeConfiguration)
        {
             if (Configuration != null)
                Configuration = DBriizeConfiguration;
             else
                 throw new Exception("DBriize.DBriizeEngine.DBriizeEngine: please supply DBriizeConfiguration");
            
            //Setting up in backup DBriizeFolderName, there must be found at least TransJournal and Scheme.
            //Configuration.Backup.SynchronizeBackup has more information
            if (Configuration.Backup.IsActive)
            {
                Configuration.Backup.DBriizeFolderName = Configuration.DBriizeDataFolderName;

                ////Running backup synchronization
                //Configuration.Backup.SynchronizeBackup();
            }
                        
            if (DBriizeConfiguration.Storage == DBriizeConfiguration.eStorage.RemoteInstance && !RemoteEngine)
                throw new Exception("DBriize.DBriizeEngine.DBriizeEngine: remote instance must be initiated via new DBriizeRemoteEngine");

            MainFolder = Configuration.DBriizeDataFolderName;

            InitDb();

            //Console.WriteLine("DBriize notification: Don't forget in the dispose function of your DLL or main application thread");
            //Console.WriteLine("                      to dispose DBriize engine:  if(_engine != null) _engine.Dispose(); ");
            //Console.WriteLine("                      to get graceful finilization of all working threads! ");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="DBriizeDataFolderName"></param>
        public DBriizeEngine(string DBriizeDataFolderName)
        {
            MainFolder = DBriizeDataFolderName;
            Configuration.DBriizeDataFolderName = DBriizeDataFolderName;

            InitDb();

            //Console.WriteLine("DBriize notification: Don't forget in the dispose function of your DLL or main application thread");
            //Console.WriteLine("                      to dispose DBriize engine:  if(_engine != null) _engine.Dispose(); ");
            //Console.WriteLine("                      to get graceful finilization of all working threads! ");
        }

        object lock_initDb = new object();

        /// <summary>
        /// InitDb
        /// </summary>
        private void InitDb()
        {
            //trying to check and create folder
            

            try
            {
                lock (lock_initDb)
                {
                    //Init type converter
                    DataTypes.DataTypesConvertor.InitDict();

                    if (Configuration.Storage == DBriizeConfiguration.eStorage.DISK)
                    {
                        DirectoryInfo di = new DirectoryInfo(MainFolder);

                        if (!di.Exists)
                            di.Create();
                    }

                    //trying to open schema file
                    DBriizeSchema = new Scheme(this);

                    //Initializing Transactions Coordinator
                    _transactionsCoordinator = new TransactionsCoordinator(this);

                    //Initializing transactions Journal, may be later move journal into transactionsCoordinator
                    //We must create journal after Schema, for getting path to rollback files
                    _transactionsJournal = new TransactionsJournal(this);

                    //Initializes transaction locker, who can help block tables of writing and reading threads
                    _transactionTablesLocker = new TransactionTablesLocker();

                    //Initializing 
                    DeferredIndexer = new TextDeferredIndexer(this);
                }
            }
            catch (Exception ex)
            {
                DBisOperable = false;
                DBisOperableReason = "InitDb";
                throw DBriizeException.Throw(DBriizeException.eDBriizeExceptions.CREATE_DB_FOLDER_FAILED, ex);
            }

            
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            if (System.Threading.Interlocked.CompareExchange(ref disposed, 1, 0) != 0)
                return;

            //if (Disposed)
            //    return;

            DBisOperable = false;
            DBisOperableReason = "DBreezeEngine.Dispose";
            //Disposed = true;

            //Disposing all transactions
            _transactionsCoordinator.StopEngine();

            //Disposing Schema
            DBriizeSchema.Dispose();

            //Disposing Trnsactional Journal, may be later move journal into transactionsCoordinator
            _transactionsJournal.Dispose();

           //Disposing Configuration
            Configuration.Dispose();
            
            //MUST BE IN THE END OF ALL.Disposing transaction locker
            _transactionTablesLocker.Dispose();

            //Disposing DeferredIndexer
            DeferredIndexer.Dispose();
        }


        /// <summary>
        /// Returns transaction object.
        /// </summary>
        /// <returns></returns>
        public Transaction GetTransaction()
        {
            if (!DBisOperable)
                throw DBriizeException.Throw(DBriizeException.eDBriizeExceptions.DB_IS_NOT_OPERABLE,DBisOperableReason,new Exception());              

            //User receives new transaction from the engine
            return this._transactionsCoordinator.GetTransaction(0, eTransactionTablesLockTypes.SHARED);

        }

        /// <summary>
        /// Returns transaction object.
        /// </summary>
        /// <param name="tablesLockType">
        /// <para>SHARED: threads can use listed tables in parallel. Must be used together with tran.SynchronizeTables command, if necessary.</para>
        /// <para>EXCLUSIVE: if other threads use listed tables for reading or writing, current thread will be in a waiting queue.</para>
        /// </param>
        /// <param name="tables"></param>
        /// <returns>Returns transaction object</returns>
        public Transaction GetTransaction(eTransactionTablesLockTypes tablesLockType, params string[] tables)
        {
            if (!DBisOperable)
                throw DBriizeException.Throw(DBriizeException.eDBriizeExceptions.DB_IS_NOT_OPERABLE, DBisOperableReason, new Exception());

            //User receives new transaction from the engine
            return this._transactionsCoordinator.GetTransaction(1, tablesLockType, tables);

        }

       
        /// <summary>
        /// Returns DBriize schema object
        /// </summary>
        public Scheme Scheme
        {
            get
            {
                return this.DBriizeSchema;
            }
        }

        /// <summary>
        /// Notifier about background events.
        /// </summary>
        /// <param name="noti"></param>
        /// <param name="obj"></param>
        internal void BackgroundNotify(string noti, object obj)
        {
            if (BackgroundTasksExternalNotifier != null)
            {
                Action a = () =>
                {
                    try
                    {
                        BackgroundTasksExternalNotifier(noti, obj);
                    }
                    catch
                    {
                    }
                };

#if NET35 || NETr40   //The same must be use for .NET 4.0

                new System.Threading.Thread(new System.Threading.ThreadStart(a)).Start();

                //new System.Threading.Thread(new System.Threading.ThreadStart(() =>
                //{
                //    a();
                //})).Start();
#else
                System.Threading.Tasks.Task.Run(a);
#endif

//#if NET35 || NETr40   //The same must be use for .NET 4.0

//                                            new System.Threading.Thread(new System.Threading.ThreadStart(() =>
//                                            {
//                                                a();
//                                            })).Start(); 
//#else
//                System.Threading.Tasks.Task.Run(() => {
//                    a();
//                });
//#endif
            }
        }

    }//end of class

}
