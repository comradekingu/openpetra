//
// DO NOT REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
//
// @Authors:
//       christiank, timop
//
// Copyright 2004-2019 by OM International
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
//
using System;
using System.Data;

using Ict.Common;
using Ict.Common.Data;
using Ict.Common.Verification;
using Ict.Petra.Shared;

namespace Ict.Petra.Shared.MCommon.Validation
{
    /// <summary>
    /// Contains helper functions for the shared validation of data, specific to Controls.
    /// </summary>
    public static class TSharedValidationControlHelper
    {
        /// <summary>
        /// Checks wheter a given DateTime is an invalid date. A check whether it is an undefined DateTime is always performed.
        /// </summary>
        /// <returns>Null if validation succeeded, otherwise a <see cref="TVerificationResult" /> is
        /// returned that contains details about the problem.</returns>
        public static TVerificationResult IsNotInvalidDate(DateTime? ADate, String ADescription,
            TVerificationResultCollection AVerificationResultCollection, bool ATreatNullAsInvalid = false,
            object AResultContext = null, System.Data.DataColumn AResultColumn = null)
        {
            return TDateChecks.IsNotUndefinedDateTime(ADate,
                ADescription, ATreatNullAsInvalid, AResultContext, AResultColumn);
        }
    }
}
