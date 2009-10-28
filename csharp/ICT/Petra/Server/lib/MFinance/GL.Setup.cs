﻿/*************************************************************************
 *
 * DO NOT REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
 *
 * @Authors:
 *       timop
 *
 * Copyright 2004-2009 by OM International
 *
 * This file is part of OpenPetra.org.
 *
 * OpenPetra.org is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * OpenPetra.org is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with OpenPetra.org.  If not, see <http://www.gnu.org/licenses/>.
 *
 ************************************************************************/
using System;
using System.Collections.Specialized;
using System.Data;
using System.Data.Odbc;
using System.Xml;
using System.IO;
using Ict.Common;
using Ict.Common.IO;
using Ict.Common.DB;
using Ict.Common.Verification;
using Ict.Petra.Shared.MFinance.Account.Data;
using Ict.Petra.Shared.MFinance.GL.Data;
using Ict.Petra.Server.MFinance.Account.Data.Access;

namespace Ict.Petra.Server.MFinance.GL.WebConnectors
{
    /// <summary>
    /// setup the account hierarchy, cost centre hierarchy, and other data relevant for a General Ledger
    /// </summary>
    public class TGLSetupWebConnector
    {
        /// <summary>
        /// returns all account hierarchies available for this ledger
        /// </summary>
        /// <param name="ALedgerNumber"></param>
        /// <returns></returns>
        public static GLSetupTDS LoadAccountHierarchies(Int32 ALedgerNumber)
        {
            GLSetupTDS MainDS = new GLSetupTDS();

            AAccountHierarchyAccess.LoadViaALedger(MainDS, ALedgerNumber, null);
            AAccountHierarchyDetailAccess.LoadViaALedger(MainDS, ALedgerNumber, null);
            AAccountAccess.LoadViaALedger(MainDS, ALedgerNumber, null);

            // Accept row changes here so that the Client gets 'unmodified' rows
            MainDS.AcceptChanges();

            // Remove all Tables that were not filled with data before remoting them.
            MainDS.RemoveEmptyTables();

            return MainDS;
        }

        /// <summary>
        /// returns cost centre hierarchy and cost centre details for this ledger
        /// </summary>
        /// <param name="ALedgerNumber"></param>
        /// <returns></returns>
        public static GLSetupTDS LoadCostCentreHierarchy(Int32 ALedgerNumber)
        {
            GLSetupTDS MainDS = new GLSetupTDS();

            ACostCentreAccess.LoadViaALedger(MainDS, ALedgerNumber, null);

            // Accept row changes here so that the Client gets 'unmodified' rows
            MainDS.AcceptChanges();

            // Remove all Tables that were not filled with data before remoting them.
            MainDS.RemoveEmptyTables();

            return MainDS;
        }

        /// <summary>
        /// save modified account hierarchy etc; does not support moving accounts;
        /// also used for saving cost centre hierarchy and cost centre details
        /// </summary>
        /// <param name="AInspectDS"></param>
        /// <param name="AVerificationResult"></param>
        /// <returns></returns>
        public static TSubmitChangesResult SaveGLSetupTDS(ref GLSetupTDS AInspectDS,
            out TVerificationResultCollection AVerificationResult)
        {
            TDBTransaction SubmitChangesTransaction;
            TSubmitChangesResult SubmissionResult = TSubmitChangesResult.scrError;

            AVerificationResult = null;

            if (AInspectDS != null)
            {
                AVerificationResult = new TVerificationResultCollection();
                SubmitChangesTransaction = DBAccess.GDBAccessObj.BeginTransaction(IsolationLevel.Serializable);
                try
                {
                    if ((AInspectDS.AAccount != null) || (AInspectDS.AAccountHierarchyDetail != null))
                    {
                        // this only supports adding new accounts, deleting unused accounts, and modifying details; but not renaming accounts
                        if (AAccountAccess.SubmitChanges(AInspectDS.AAccount, SubmitChangesTransaction,
                                out AVerificationResult))
                        {
                            if (AAccountHierarchyDetailAccess.SubmitChanges(AInspectDS.AAccountHierarchyDetail, SubmitChangesTransaction,
                                    out AVerificationResult))
                            {
                                SubmissionResult = TSubmitChangesResult.scrOK;
                            }
                            else
                            {
                                SubmissionResult = TSubmitChangesResult.scrError;
                            }
                        }
                        else
                        {
                            SubmissionResult = TSubmitChangesResult.scrError;
                        }
                    }
                    else if (AInspectDS.ACostCentre.Count > 0)
                    {
                        // this only supports adding new cost centres, and modifying details; but not renaming cost centres
                        if (ACostCentreAccess.SubmitChanges(AInspectDS.ACostCentre, SubmitChangesTransaction,
                                out AVerificationResult))
                        {
                            SubmissionResult = TSubmitChangesResult.scrOK;
                        }
                        else
                        {
                            SubmissionResult = TSubmitChangesResult.scrError;
                        }
                    }

                    if (SubmissionResult == TSubmitChangesResult.scrOK)
                    {
                        DBAccess.GDBAccessObj.CommitTransaction();
                    }
                    else
                    {
                        DBAccess.GDBAccessObj.RollbackTransaction();
                    }
                }
                catch (Exception e)
                {
                    TLogging.Log("after submitchanges: exception " + e.Message);

                    DBAccess.GDBAccessObj.RollbackTransaction();

                    throw new Exception(e.ToString() + " " + e.Message);
                }
            }

            return SubmissionResult;
        }

        /// <summary>
        /// helper function for ExportAccountHierarchy
        /// </summary>
        private static void InsertNodeIntoXmlDocument(GLSetupTDS AMainDS,
            XmlDocument ADoc,
            XmlNode AParentNode,
            AAccountHierarchyDetailRow ADetailRow)
        {
            AAccountRow account = (AAccountRow)AMainDS.AAccount.Rows.Find(new object[] { ADetailRow.LedgerNumber, ADetailRow.ReportingAccountCode });
            XmlElement accountNode = ADoc.CreateElement(ADetailRow.ReportingAccountCode);

            // AccountCodeToReportTo and ReportOrder are encoded implicitly
            accountNode.SetAttribute("active", account.AccountActiveFlag ? "True" : "False");
            accountNode.SetAttribute("type", account.AccountType.ToString());
            accountNode.SetAttribute("debitcredit", account.DebitCreditIndicator ? "debit" : "credit");
            accountNode.SetAttribute("validcc", account.ValidCcCombo);
            accountNode.SetAttribute("shortdesc", account.EngAccountCodeShortDesc);

            if (account.EngAccountCodeLongDesc != account.EngAccountCodeShortDesc)
            {
                accountNode.SetAttribute("longdesc", account.EngAccountCodeLongDesc);
            }

            if (account.EngAccountCodeShortDesc != account.AccountCodeShortDesc)
            {
                accountNode.SetAttribute("localdesc", account.AccountCodeShortDesc);
            }

            if (account.EngAccountCodeLongDesc != account.AccountCodeLongDesc)
            {
                accountNode.SetAttribute("locallongdesc", account.AccountCodeLongDesc);
            }

            AParentNode.AppendChild(accountNode);

            AMainDS.AAccountHierarchyDetail.DefaultView.Sort = AAccountHierarchyDetailTable.GetReportOrderDBName();
            AMainDS.AAccountHierarchyDetail.DefaultView.RowFilter =
                AAccountHierarchyDetailTable.GetAccountHierarchyCodeDBName() + " = '" + ADetailRow.AccountHierarchyCode + "' AND " +
                AAccountHierarchyDetailTable.GetAccountCodeToReportToDBName() + " = '" + ADetailRow.ReportingAccountCode + "'";

            foreach (DataRowView rowView in AMainDS.AAccountHierarchyDetail.DefaultView)
            {
                AAccountHierarchyDetailRow accountDetail = (AAccountHierarchyDetailRow)rowView.Row;
                InsertNodeIntoXmlDocument(AMainDS, ADoc, accountNode, accountDetail);
            }
        }

        /// <summary>
        /// return a simple XMLDocument (encoded into a string) with the account hierarchy and account details;
        /// root account can be calculated (find which account is reporting nowhere)
        /// </summary>
        /// <param name="ALedgerNumber"></param>
        /// <param name="AAccountHierarchyName"></param>
        /// <returns></returns>
        public static string ExportAccountHierarchy(Int32 ALedgerNumber, string AAccountHierarchyName)
        {
            GLSetupTDS MainDS = new GLSetupTDS();

            AAccountHierarchyAccess.LoadViaALedger(MainDS, ALedgerNumber, null);
            AAccountHierarchyDetailAccess.LoadViaALedger(MainDS, ALedgerNumber, null);
            AAccountAccess.LoadViaALedger(MainDS, ALedgerNumber, null);

            XmlDocument doc = TYml2Xml.CreateXmlDocument();

            AAccountHierarchyRow accountHierarchy = (AAccountHierarchyRow)MainDS.AAccountHierarchy.Rows.Find(new object[] { ALedgerNumber,
                                                                                                                            AAccountHierarchyName });

            if (accountHierarchy != null)
            {
                // find the BALSHT account that is reporting to the root account
                MainDS.AAccountHierarchyDetail.DefaultView.RowFilter =
                    AAccountHierarchyDetailTable.GetAccountHierarchyCodeDBName() + " = '" + AAccountHierarchyName + "' AND " +
                    AAccountHierarchyDetailTable.GetAccountCodeToReportToDBName() + " = '" + accountHierarchy.RootAccountCode + "'";

                InsertNodeIntoXmlDocument(MainDS, doc, doc.DocumentElement,
                    (AAccountHierarchyDetailRow)MainDS.AAccountHierarchyDetail.DefaultView[0].Row);
            }

            // XmlDocument is not serializable, therefore print it to string and return the string
            return TXMLParser.XmlToString(doc);
        }

        /// <summary>
        /// helper function for ExportCostCentreHierarchy
        /// </summary>
        private static void InsertNodeIntoXmlDocument(GLSetupTDS AMainDS,
            XmlDocument ADoc,
            XmlNode AParentNode,
            ACostCentreRow ADetailRow)
        {
            XmlElement costCentreNode = ADoc.CreateElement(ADetailRow.CostCentreName);

            // CostCentreToReportTo is encoded implicitly
            costCentreNode.SetAttribute("code", ADetailRow.CostCentreCode);
            costCentreNode.SetAttribute("active", ADetailRow.CostCentreActiveFlag ? "True" : "False");
            costCentreNode.SetAttribute("type", ADetailRow.CostCentreType.ToString());
            AParentNode.AppendChild(costCentreNode);

            AMainDS.ACostCentre.DefaultView.Sort = ACostCentreTable.GetCostCentreCodeDBName();
            AMainDS.ACostCentre.DefaultView.RowFilter =
                ACostCentreTable.GetCostCentreToReportToDBName() + " = '" + ADetailRow.CostCentreCode + "'";

            foreach (DataRowView rowView in AMainDS.ACostCentre.DefaultView)
            {
                InsertNodeIntoXmlDocument(AMainDS, ADoc, costCentreNode, (ACostCentreRow)rowView.Row);
            }
        }

        /// <summary>
        /// return a simple XMLDocument (encoded into a string) with the cost centre hierarchy and cost centre details;
        /// </summary>
        /// <param name="ALedgerNumber"></param>
        /// <returns></returns>
        public static string ExportCostCentreHierarchy(Int32 ALedgerNumber)
        {
            GLSetupTDS MainDS = new GLSetupTDS();

            ACostCentreAccess.LoadViaALedger(MainDS, ALedgerNumber, null);

            XmlDocument doc = TYml2Xml.CreateXmlDocument();

            MainDS.ACostCentre.DefaultView.RowFilter =
                ACostCentreTable.GetCostCentreToReportToDBName() + " IS NULL";

            InsertNodeIntoXmlDocument(MainDS, doc, doc.DocumentElement,
                (ACostCentreRow)MainDS.ACostCentre.DefaultView[0].Row);

            // XmlDocument is not serializable, therefore print it to string and return the string
            return TXMLParser.XmlToString(doc);
        }

        private static void CreateAccountHierarchyRecursively(ref GLSetupTDS AMainDS,
            ref StringCollection AImportedAccountNames,
            XmlNode ACurrentNode,
            string AParentAccountCode)
        {
            AAccountRow newAccount = null;

            // reverse adjustments so that name with space or starting with digit works with XML
            string AccountCode = ACurrentNode.Name.Replace("SPACE", " ").Replace("DIGIT", "");

            AImportedAccountNames.Add(AccountCode);

            // does this account already exist?
            bool newRow = false;
            DataRow existingAccount = AMainDS.AAccount.Rows.Find(new object[] { AMainDS.AAccountHierarchy[0].LedgerNumber, AccountCode });

            if (existingAccount != null)
            {
                newAccount = (AAccountRow)existingAccount;
            }
            else
            {
                newRow = true;
                newAccount = AMainDS.AAccount.NewRowTyped();
            }

            newAccount.LedgerNumber = AMainDS.AAccountHierarchy[0].LedgerNumber;
            newAccount.AccountCode = AccountCode;
            newAccount.AccountActiveFlag = TYml2Xml.GetAttributeRecursive(ACurrentNode, "active").ToLower() == "true";
            newAccount.AccountType = TYml2Xml.GetAttributeRecursive(ACurrentNode, "type");
            newAccount.DebitCreditIndicator = TYml2Xml.GetAttributeRecursive(ACurrentNode, "debitcredit") == "debit";
            newAccount.ValidCcCombo = TYml2Xml.GetAttributeRecursive(ACurrentNode, "validcc");
            newAccount.EngAccountCodeShortDesc = TYml2Xml.GetAttributeRecursive(ACurrentNode, "shortdesc");

            if (TXMLParser.HasAttribute(ACurrentNode, "shortdesc"))
            {
                newAccount.EngAccountCodeLongDesc = TYml2Xml.GetAttribute(ACurrentNode, "longdesc");
            }
            else
            {
                newAccount.EngAccountCodeLongDesc = TYml2Xml.GetAttributeRecursive(ACurrentNode, "longdesc");
            }

            if (newAccount.EngAccountCodeLongDesc.Length == 0)
            {
                newAccount.EngAccountCodeLongDesc = newAccount.EngAccountCodeShortDesc;
            }

            newAccount.AccountCodeShortDesc = TYml2Xml.GetAttribute(ACurrentNode, "localdesc");
            newAccount.AccountCodeLongDesc = TYml2Xml.GetAttribute(ACurrentNode, "locallongdesc");

            if (newAccount.AccountCodeShortDesc.Length == 0)
            {
                newAccount.AccountCodeShortDesc = newAccount.EngAccountCodeShortDesc;
            }

            if (newAccount.AccountCodeLongDesc.Length == 0)
            {
                newAccount.AccountCodeLongDesc = newAccount.AccountCodeShortDesc;
            }

            if (newRow)
            {
                AMainDS.AAccount.Rows.Add(newAccount);
            }

            // account hierarchy has been deleted, so always add
            AAccountHierarchyDetailRow newAccountHDetail = AMainDS.AAccountHierarchyDetail.NewRowTyped();
            newAccountHDetail.LedgerNumber = AMainDS.AAccountHierarchy[0].LedgerNumber;
            newAccountHDetail.AccountHierarchyCode = AMainDS.AAccountHierarchy[0].AccountHierarchyCode;
            newAccountHDetail.AccountCodeToReportTo = AParentAccountCode;
            newAccountHDetail.ReportingAccountCode = AccountCode;

            AMainDS.AAccountHierarchyDetail.Rows.Add(newAccountHDetail);

            foreach (XmlNode child in ACurrentNode.ChildNodes)
            {
                CreateAccountHierarchyRecursively(ref AMainDS, ref AImportedAccountNames, child, newAccount.AccountCode);
            }
        }

        /// <summary>
        /// only works if there are no balances/transactions yet for the accounts that are deleted
        /// </summary>
        /// <param name="ALedgerNumber"></param>
        /// <param name="AHierarchyName"></param>
        /// <param name="AXmlAccountHierarchy"></param>
        /// <returns></returns>
        public static bool ImportAccountHierarchy(Int32 ALedgerNumber, string AHierarchyName, string AXmlAccountHierarchy)
        {
            XmlDocument doc = new XmlDocument();

            doc.LoadXml(AXmlAccountHierarchy);

            GLSetupTDS MainDS = LoadAccountHierarchies(ALedgerNumber);
            XmlNode root = doc.FirstChild.NextSibling.FirstChild;

            StringCollection ImportedAccountNames = new StringCollection();

            // delete all account hierarchy details of this hierarchy
            foreach (AAccountHierarchyDetailRow accounthdetail in MainDS.AAccountHierarchyDetail.Rows)
            {
                if (accounthdetail.AccountHierarchyCode == AHierarchyName)
                {
                    accounthdetail.Delete();
                }
            }

            CreateAccountHierarchyRecursively(ref MainDS, ref ImportedAccountNames, root, MainDS.AAccountHierarchy[0].LedgerNumber.ToString());

            foreach (AAccountRow accountRow in MainDS.AAccount.Rows)
            {
                if (!ImportedAccountNames.Contains(accountRow.AccountCode))
                {
                    // TODO: delete accounts that don't exist anymore in the new hierarchy, or deactivate them?
                    //       but need to raise a failure because they are missing in the account hierarcy
                    // (check if their balance is empty and no transactions exist, or catch database constraint violation)
                    // TODO: what about system accounts? probably alright to ignore here

                    accountRow.Delete();
                }
            }

            TVerificationResultCollection VerificationResult;
            GLSetupTDS InspectDS = MainDS.GetChangesTyped(true);
            return SaveGLSetupTDS(ref InspectDS, out VerificationResult) == TSubmitChangesResult.scrOK;
        }

        /// <summary>
        /// TODO import cost centre hierarchy
        /// </summary>
        public static bool ImportCostCentreHierarchy(Int32 ALedgerNumber, string AXmlHierarchy)
        {
            return false;
        }

        /// <summary>
        /// import basic data for new ledger
        /// </summary>
        public static bool ImportNewLedger(Int32 ALedgerNumber,
            string AXmlAccountHierarchy,
            string AXmlCostCentreHierarchy,
            string AXmlInitialBalances)
        {
            // TODO ImportNewLedger
            return false;
        }

        /// <summary>
        /// check if it is possible to delete the account (has no transactions)
        /// </summary>
        /// <param name="ALedgerNumber"></param>
        /// <param name="AAccountCode"></param>
        /// <returns>false if account cannot be deleted</returns>
        public static bool CanDeleteAccount(Int32 ALedgerNumber, string AAccountCode)
        {
            // TODO: enhance sql statement to check for more references to a_account
            string SqlStmt = TDataBase.ReadSqlFile("GL.Setup.CheckAccountReferences.sql");

            OdbcParameter[] parameters = new OdbcParameter[2];
            parameters[0] = new OdbcParameter("LedgerNumber", OdbcType.Date);
            parameters[0].Value = ALedgerNumber;
            parameters[1] = new OdbcParameter("AccountCode", OdbcType.VarChar);
            parameters[1].Value = AAccountCode;
            return Convert.ToInt32(DBAccess.GDBAccessObj.ExecuteScalar(SqlStmt, null, false, parameters)) == 0;
        }
    }
}