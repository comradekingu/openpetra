﻿//
// DO NOT REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
//
// @Authors:
//       wolfgangu
//
// Copyright 2004-2010 by OM International
//
// This file is part of OpenPetra.org.
//
// OpenPetra.org is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// OpenPetra.org is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with OpenPetra.org.  If not, see <http://www.gnu.org/licenses/>.
using System;
using System.Text.RegularExpressions;

using Ict.Petra.Server.MFinance.Account.Data.Access;
using Ict.Petra.Shared.MFinance.Account.Data;
using Ict.Petra.Server.MFinance.GL.WebConnectors;
using Ict.Petra.Shared.MCommon.Data;
using Ict.Petra.Server.MCommon.Data.Access;


using Ict.Common;
using Ict.Common.DB;
using Ict.Common.Verification;
using Ict.Petra.Server.App.ClientDomain;
using Ict.Petra.Server.App.Core.Security;
using Ict.Petra.Server.MFinance.Gift.Data.Access;
using Ict.Petra.Server.MFinance.GL;
using Ict.Petra.Server.MPartner.Partner.Data.Access;
using Ict.Petra.Shared;
using Ict.Petra.Shared.MFinance;
using Ict.Petra.Shared.MFinance.Gift.Data;
using Ict.Petra.Shared.MFinance.GL.Data;
using Ict.Petra.Shared.MPartner.Partner.Data;

namespace Ict.Petra.Server.MFinance.GL.WebConnectors
{
    /// <summary>
    /// Description of GL_Revaluation.
    /// </summary>
    public class TRevaluationWebConnector
    {
        /// <summary>
        /// Main Revalutate Routine!
        /// A single call of this routine creates a batch, a journal and a twin set of transactions
        /// for each account number - cost center combination which holds a foreign currency value
        /// </summary>
        /// <param name="ALedgerNum">Number of the Ledger to be revaluated</param>
        /// <param name="ABaseCurrencyType">Type of the base currency EUR USD or else</param>
        /// <param name="ARevaluationAccount">Offset Account for the revaluation</param>
        /// <param name="ARevaluationCostCenter">Cost Center for the revaluation</param>
        /// <param name="AForeignCurrency">Types (Array) of the foreign currency account</param>
        /// <param name="ANewExchangeRate">Array of the exchange rates</param>
        /// <returns></returns>


        [RequireModulePermission("FINANCE-1")]
        public static bool Revaluate(int ALedgerNum,
            string ABaseCurrencyType,
            string ARevaluationAccount,
            string ARevaluationCostCenter,
            string[] AForeignCurrency,
            decimal[] ANewExchangeRate)
        {
            CLSRevaluation revaluation = new CLSRevaluation(ALedgerNum,
                ABaseCurrencyType, ARevaluationAccount, ARevaluationCostCenter,
                AForeignCurrency, ANewExchangeRate);

            revaluation.RunRevaluation();
            return true;
        }
    }

    public class CLSRevaluation
    {
        private int intLedgerNum;
        private string strBaseCurrencyType;
        private string strRevaluationAccount;
        private string strRevaluationCostCenter;
        private string[] strArrForeignCurrencyType;
        private decimal[] decArrExchangeRate;

        private int intPtrToForeignData;


        decimal decAccActForeign;
        decimal decAccActBase;

        decimal decAccActBaseRequired;

        decimal decDelta;

        private GLBatchTDS GLDataset = null;
        private ABatchRow batch;
        private AJournalRow journal;

        public CLSRevaluation(int ALedgerNum,
            string ABaseCurrencyType,
            string ARevaluationAccount,
            string ARevaluationCostCenter,
            string[] AForeignCurrency,
            decimal[] ANewExchangeRate)
        {
            intLedgerNum = ALedgerNum;
            strBaseCurrencyType = ABaseCurrencyType;
            strRevaluationAccount = ARevaluationAccount;
            strRevaluationCostCenter = ARevaluationCostCenter;
            strArrForeignCurrencyType = AForeignCurrency;
            decArrExchangeRate = ANewExchangeRate;
        }

        public void RunRevaluation()
        {
            AAccountTable accountTable =
                AAccountAccess.LoadViaALedger(intLedgerNum, null);
            AGeneralLedgerMasterTable generalLedgerMasterTable =
                AGeneralLedgerMasterAccess.LoadViaALedger(intLedgerNum, null);

            for (int iCnt = 0; iCnt < accountTable.Rows.Count; ++iCnt)
            {
                AAccountRow accountRow = (AAccountRow)accountTable[iCnt];

                // Account shall be active
                if (accountRow.AccountActiveFlag)
                {
                    // Account shall hold foreign Currency values
                    if (accountRow.ForeignCurrencyFlag)
                    {
                        string strAccountCode = accountRow.AccountCode;

                        for (int kCnt = 0; kCnt < strArrForeignCurrencyType.Length; ++kCnt)
                        {
                            intPtrToForeignData = kCnt;

                            // AForeignCurrency[] and ANewExchangeRate[] shall support a value
                            // for this account resp. for the currency of the account
                            if (accountRow.ForeignCurrencyCode.Equals(strArrForeignCurrencyType[kCnt]))
                            {
                                for (int jCnt = 0; jCnt < generalLedgerMasterTable.Rows.Count; ++jCnt)
                                {
                                    AGeneralLedgerMasterRow generalLedgerMasterRow =
                                        (AGeneralLedgerMasterRow)generalLedgerMasterTable[jCnt];

                                    // generalLedgerMaster shall support Entries for this account
                                    if (generalLedgerMasterRow.AccountCode.Equals(strAccountCode))
                                    {
                                        // Account is localized ...
                                        RevaluateAccount(accountRow.AccountCode);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void RevaluateAccount(string ARelevantAccount)
        {
            AGeneralLedgerMasterTable generalLedgerMasterTable =
                AGeneralLedgerMasterAccess.LoadViaAAccount(intLedgerNum, ARelevantAccount, null);

            for (int iCnt = 0; iCnt < generalLedgerMasterTable.Rows.Count; ++iCnt)
            {
                AGeneralLedgerMasterRow generalLedgerMasterRow =
                    (AGeneralLedgerMasterRow)generalLedgerMasterTable[iCnt];

                decAccActForeign = generalLedgerMasterRow.YtdActualForeign;
                decAccActBase = generalLedgerMasterRow.YtdActualBase;

                decAccActBaseRequired = generalLedgerMasterRow.YtdActualForeign /
                                        decArrExchangeRate[intPtrToForeignData];

                decDelta = decAccActBase - decAccActBaseRequired;
                
                System.Diagnostics.Debug.WriteLine("delta: " + decDelta.ToString());

                // decDelta ... shall not be zero otherwise a revaluation is senseless
                if (decDelta != 0)
                {

                	System.Diagnostics.Debug.WriteLine("xxx");
                    // Now we have the relevant Cost Center ...
                    RevaluateCostCenter(ARelevantAccount, generalLedgerMasterRow.CostCentreCode);
                }
            }
        }

        private void RevaluateCostCenter(string ARelevantAccount, string ACostCenter)
        {
        	// In the very first run Batch and Journal shall be created ...
            if (GLDataset == null)
            {
                InitBatchAndJournal();
            }
        	int intNoOfForeignDigts = new GetCurrencyInfo(
            	strArrForeignCurrencyType[intPtrToForeignData]).digits;
            CreateTransaction();
            CreateTransaction();
            System.Diagnostics.Debug.WriteLine("yyy");
        }

        private void InitBatchAndJournal()
        {
            GLDataset = TTransactionWebConnector.CreateABatch(intLedgerNum);
            batch = GLDataset.ABatch[0];
            batch.BatchDescription = Catalog.GetString("Period end revaluations");
            //batch.DateEffective = new
            //	GetAccountingPeriodInfo(intLedgerNum).GetEffectiveDateOfPeriod(..);
            batch.BatchStatus = MFinanceConstants.BATCH_UNPOSTED;

            journal = GLDataset.AJournal.NewRowTyped();
            journal.LedgerNumber = batch.LedgerNumber;
            journal.BatchNumber = batch.BatchNumber;
            journal.JournalNumber = 1;
            journal.DateEffective = batch.DateEffective;
            //journal.JournalPeriod = giftbatch.BatchPeriod;
            //journal.TransactionCurrency = giftbatch.CurrencyCode;
            journal.JournalDescription = batch.BatchDescription;
            journal.TransactionTypeCode = MFinanceConstants.TRANSACTION_FX_REVAL;
            journal.SubSystemCode = MFinanceConstants.SUB_SYSTEM_GL;
            journal.LastTransactionNumber = 0;
            journal.DateOfEntry = DateTime.Now;
            journal.ExchangeRateToBase = 1.0M;
            //GLDataset.AJournal.Rows.Add(journal);
            
 
//            TVerificationResultCollection AVerifications;
//            System.Diagnostics.Debug.WriteLine("Saved:" + (Ict.Petra.Server.MFinance.GL.WebConnectors.TTransactionWebConnector.SaveGLBatchTDS(
//            	ref GLDataset, out AVerifications) == TSubmitChangesResult.scrOK).ToString());
        }

        private void CreateTransaction()
        {
            ATransactionRow transaction = null;

            transaction = GLDataset.ATransaction.NewRowTyped();
            transaction.LedgerNumber = journal.LedgerNumber;
            transaction.BatchNumber = journal.BatchNumber;
            transaction.JournalNumber = journal.JournalNumber;
            transaction.TransactionNumber = ++journal.LastTransactionNumber;
            //transaction.AccountCode = giftdetail.AccountCode;
            //transaction.CostCentreCode = giftdetail.CostCentreCode;
            //transaction.Narrative = "GB - Gift Batch " + giftbatch.BatchNumber.ToString();
            //transaction.Reference = "GB" + giftbatch.BatchNumber.ToString();
            transaction.DebitCreditIndicator = false;
            transaction.TransactionAmount = 0;
            transaction.AmountInBaseCurrency = 0;
            //transaction.TransactionDate = giftbatch.GlEffectiveDate;

            //GLDataset.ATransaction.Rows.Add(transaction);
        }
    }

    
    /// <summary>
    /// Gets the specific date informations of an accounting intervall. 
    /// </summary>
    public class GetAccountingPeriodInfo
    {
        private AAccountingPeriodTable periodTable = null;

        /// <summary>
        /// Constructor needs a valid ledger number. 
        /// </summary>
        /// <param name="ALedgerNumber">Ledger number</param>
        public GetAccountingPeriodInfo(int ALedgerNumber)
        {
            periodTable = AAccountingPeriodAccess.LoadViaALedger(ALedgerNumber, null);
        }

        /// <summary>
        /// Selects to correct AAccountingPeriodRow or - in case of an error - 
        /// it sets to null
        /// </summary>
        /// <param name="APeriodNum">Number of the requested period</param>
        /// <returns></returns>
        private AAccountingPeriodRow GetRowOfPeriod(int APeriodNum)
        {
            if (periodTable != null)
            {
                if (periodTable.Rows.Count != 0)
                {
                    for (int i = 0; i < periodTable.Rows.Count; ++i)
                    {
                        AAccountingPeriodRow periodRow =
                            (AAccountingPeriodRow)periodTable[i];

                        if (periodRow.AccountingPeriodNumber == APeriodNum)
                        {
                            return periodRow;
                        }
                    }

                    return null;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Reads the effective date of the period
        /// </summary>
        /// <param name="APeriodNum">The number of the period. DateTime.MinValue is an
        /// error value.</param>
        /// <returns></returns>
        public DateTime GetEffectiveDateOfPeriod(int APeriodNum)
        {
            AAccountingPeriodRow periodRow = GetRowOfPeriod(APeriodNum);

            if (periodRow != null)
            {
                return periodRow.EffectiveDate;
            }
            else
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Reads the end date of the period
        /// </summary>
        /// <param name="APeriodNum">The number of the period. DateTime.MinValue is an
        /// error value.</param>
        /// <returns></returns>
        public DateTime GetDatePeriodEnd(int APeriodNum)
        {
            AAccountingPeriodRow periodRow = GetRowOfPeriod(APeriodNum);

            if (periodRow != null)
            {
                return periodRow.PeriodEndDate;
            }
            else
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Reads the start date of the period
        /// </summary>
        /// <param name="APeriodNum">The number of the period. DateTime.MinValue is an
        /// error value.</param>
        /// <returns></returns>
        public DateTime GetDatePeriodStart(int APeriodNum)
        {
            AAccountingPeriodRow periodRow = GetRowOfPeriod(APeriodNum);

            if (periodRow != null)
            {
                return periodRow.PeriodStartDate;
            }
            else
            {
                return DateTime.MinValue;
            }
        }
    }
}


//				System.Diagnostics.Debug.WriteLine("#########################################################");
//				System.Diagnostics.Debug.WriteLine("Ledger        : " + ALedgerNum);
//				System.Diagnostics.Debug.WriteLine("Account       : " + ARelevantAccount);
//				System.Diagnostics.Debug.WriteLine("Cost Center   : " + generalLedgerMasterRow.CostCentreCode);
//				System.Diagnostics.Debug.WriteLine("Base Currency : " +
//				                                   generalLedgerMasterRow.YtdActualBase + "[" + ABaseCurrencyType + "]");
//				System.Diagnostics.Debug.WriteLine("For. Currency : " +
//				                                   generalLedgerMasterRow.YtdActualForeign + "[" + AForeignCurrency + "]");
//				System.Diagnostics.Debug.WriteLine("Revaluation   : " + ARevaluationCostCenter + ":" + ARevaluationAccount);
//				System.Diagnostics.Debug.WriteLine("Exchange-Rate : " + ANewExchangeRate.ToString());
//				System.Diagnostics.Debug.WriteLine("#########################################################");