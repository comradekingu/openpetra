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
using System.Data;
using System.IO;
using System.Xml;
using System.Windows.Forms;
using Ict.Common;
using Ict.Common.IO;
using Ict.Common.Verification;
using Ict.Petra.Shared.Interfaces.Plugins.MFinance;
using Ict.Petra.Shared.MFinance.Account.Data;
using Mono.Unix;
using Ict.Petra.Client.App.Core.RemoteObjects;

namespace Plugin.BankImportFromCSV
{
    /// <summary>
    /// import a bank statement from a CSV file
    /// </summary>
    public class TBankImportFromCSV : IImportBankStatement
    {
        /// <summary>
        /// should return the text for the filter for AEpTransactionTable to get all the gifts, by transaction type
        /// </summary>
        /// <returns></returns>
        public string GetFilterGifts()
        {
            // TODO
            return "";
        }

        /// <summary>
        /// asks the user to open a csv file and imports the contents according to the config file
        /// </summary>
        /// <param name="AStatementKey">this returns the first key of a statement that was imported. depending on the implementation, several statements can be created from one file</param>
        /// <returns></returns>
        public bool ImportBankStatement(out Int32 AStatementKey)
        {
            OpenFileDialog DialogOpen = new OpenFileDialog();

            DialogOpen.Filter = Catalog.GetString("bank statement (*.csv)|*.csv|");
            DialogOpen.RestoreDirectory = true;
            DialogOpen.Title = Catalog.GetString("Please select the bank statement to import");

            if (DialogOpen.ShowDialog() != DialogResult.OK)
            {
                AStatementKey = -1;
                return false;
            }

            string BankStatementFilename = DialogOpen.FileName;
            StreamReader dataFile = new StreamReader(BankStatementFilename);

            string FileStructureConfig = TAppSettingsManager.GetValueStatic("BankImportCSV.FileStructure.Config", "");

            if (FileStructureConfig.Length == 0)
            {
                TLogging.Log("Missing setting in config file: BankImportCSV.FileStructure.Config");
                AStatementKey = -1;
                return false;
            }

            if (!System.IO.File.Exists(FileStructureConfig))
            {
                TLogging.Log("Cannot find file " + FileStructureConfig);
                AStatementKey = -1;
                return false;
            }

            TYml2Xml parser = new TYml2Xml(FileStructureConfig);

            XmlDocument dataDescription = parser.ParseYML2XML();
            XmlNode RootNode = TXMLParser.FindNodeRecursive(dataDescription.DocumentElement, "RootNode");

            XmlNode ColumnsNode = TXMLParser.GetChild(RootNode, "Columns");
            Int32 FirstTransactionRow = TXMLParser.GetIntAttribute(RootNode, "FirstTransactionRow");
            string CurrencyCode = TXMLParser.GetAttribute(RootNode, "Currency");
            string Separator = TXMLParser.GetAttribute(RootNode, "Separator");
            string DateFormat = TXMLParser.GetAttribute(RootNode, "DateFormat");
            string ThousandsSeparator = TXMLParser.GetAttribute(RootNode, "ThousandsSeparator");
            string DecimalSeparator = TXMLParser.GetAttribute(RootNode, "DecimalSeparator");

            // read headers
            for (Int32 lineCounter = 0; lineCounter < FirstTransactionRow - 1; lineCounter++)
            {
                dataFile.ReadLine();
            }

            // TODO: support splitting a file by month?
            // at the moment this only works for files that are already split by month
            // TODO: check if this statement has already been imported, by the stmt.Filename; delete old statement
            AEpStatementTable stmtTable = new AEpStatementTable();
            AEpStatementRow stmt = stmtTable.NewRowTyped();
            stmt.StatementKey = -1;

            // TODO: depending on the path of BankStatementFilename you could determine between several bank accounts
            stmt.BankKey = Convert.ToInt64(TXMLParser.GetAttribute(RootNode, "BankPartnerKey"));
            stmt.Filename = BankStatementFilename;
            stmt.CurrencyCode = CurrencyCode;
            stmtTable.Rows.Add(stmt);

            DateTime latestDate = DateTime.MinValue;

            AEpTransactionTable transactionsTable = new AEpTransactionTable();

            do
            {
                string line = dataFile.ReadLine();

                AEpTransactionRow row = transactionsTable.NewRowTyped();
                row.StatementKey = stmt.StatementKey;

                foreach (XmlNode ColumnNode in ColumnsNode.ChildNodes)
                {
                    string Value = StringHelper.GetNextCSV(ref line, Separator);
                    string UseAs = TXMLParser.GetAttribute(ColumnNode, "UseAs");

                    if (UseAs.ToLower() == "dateeffective")
                    {
                        row.DateEffective = XmlConvert.ToDateTime(Value, DateFormat);

                        if (row.DateEffective > latestDate)
                        {
                            latestDate = row.DateEffective;
                        }
                    }

                    if (UseAs.ToLower() == "accountname")
                    {
                        row.AccountName = Value;
                    }

                    if (UseAs.ToLower() == "description")
                    {
                        row.Description = Value;
                    }

                    if (UseAs.ToLower() == "amount")
                    {
                        if (Value.Contains(" "))
                        {
                            // cut off currency code; should have been defined in the data description file, for the whole batch
                            Value = Value.Substring(0, Value.IndexOf(" ") - 1);
                        }

                        Value = Value.Replace(ThousandsSeparator, "");
                        Value = Value.Replace(DecimalSeparator, ".");

                        row.TransactionAmount = Convert.ToDouble(Value, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }

                transactionsTable.Rows.Add(row);
            } while (!dataFile.EndOfStream);

            stmt.Date = latestDate;

            TVerificationResultCollection VerificationResult;

            if (TRemote.MFinance.ImportExport.WebConnectors.StoreNewBankStatement(stmtTable, transactionsTable,
                    out VerificationResult) == TSubmitChangesResult.scrOK)
            {
                AStatementKey = stmtTable[0].StatementKey;
            }

            AStatementKey = -1;
            return false;
        }
    }
}