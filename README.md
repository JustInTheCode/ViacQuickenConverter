# VIAC Quicken Converter

A CLI tool that converts VIAC (Swiss 3a pension) PDF statements into Quicken-compatible CSV files for easy import into Quicken.

---

## Use Cases

This tool is useful if you want to, track your VIAC 3a pension investments in Quicken for comprehensive financial planning or for US Taxes.

VIAC provides statements as individual PDFs, which would require manual data entry into Quicken. This tool automates the conversion process, saving time and reducing errors.

---

## Terminology Note

Throughout this documentation:
- **ISIN** refers to the International Securities Identification Number (the unique 12-character alphanumeric code like `CH0130595124`).
- VIAC uses the term "ISIN" in their statements.
- **Quicken uses "Symbol" for the same field** — when you see "Symbol" in Quicken, it corresponds to the ISIN from VIAC.
- The CSV output column is named `optionalSymbol` to match Quicken's import format.

---

## How to Download & Run

1. Navigate to the [download](./download) folder in this repository.
2. Select the latest version (e.g., `1.0.0`).
3. Pick the ZIP file for your operating system and CPU architecture. *(If you're unsure and on Windows, choose `win-x64.zip`.)*
4. Download and unzip the archive.
5. Run the included `ViacQuickenConverter.exe`.

No installation required — just unzip and run.

> ⚠️ **Windows SmartScreen Warning**  
> When running the `.exe` for the first time, Windows may display a SmartScreen warning:  
> _"Windows protected your PC"_.
>
> This is expected for unsigned apps. To proceed:
> 1. Click **More info**
> 2. Click **Run anyway**

---

## How to Use

1. **Download your VIAC statements:**
   - Log into your VIAC account.
   - Navigate to the "Documents" section.
   - Download all relevant PDF statements to a single folder.
   
2. **Run the converter:**
   - Launch `ViacQuickenConverter.exe`.
   - Enter the full path to the folder containing your VIAC PDF statements.
   - The tool will automatically parse all PDF files in the folder.

3. **Import into Quicken:**
   - Once processing is complete, a CSV file will be created in the same folder as the executable.
   - Open Quicken and go to **File → File Import → Banking Transactions → From (.CSV) File**.
   - Select the generated CSV file and follow the import wizard.

> 💡 **Tip:** See the [examples](./examples) folder for a sample Quicken CSV output file (`viac_quicken_2025-12-18.csv`) and Quicken's example import format (`quicken_import.csv`).

---

## Features

- Parses all VIAC statement types:
  - Exchange Settlements (buys and sells)
  - Dividend Payments and Dividend Cancellations
  - Deposits
  - Reimbursements
  - Interest Payments
  - Commissions (fees)
  - Security Mergers (fund fusions)
- Automatically converts all transactions to USD using historical exchange rates.
- Calculates share quantities based on the formula: **units = amount ÷ price**.
- Handles security mergers by updating ISINs and names across all transactions.
- Uses the newest security name and ISIN for each security to prevent Quicken from creating new securities when both change (critical for mergers).
- Prevents overwriting of previous results by auto-incrementing output file names (e.g., `viac_quicken_2025-12-18_1.csv`).
- Validates all parsed data and provides clear error messages if issues are detected.

---

## Output

- The converted CSV file is saved in the **same folder** as the executable.
- By default, the file is named `viac_quicken_YYYY-MM-DD.csv` (using the current date).
- If a file with the same name already exists, a number suffix is added automatically (e.g., `viac_quicken_2025-12-18_1.csv`).
- Existing files are **never overwritten**.

### CSV Format

The output CSV follows the Quicken import format with the following columns:

| Column          | Description                                                                 |
|-----------------|-----------------------------------------------------------------------------|
| action          | Transaction type (e.g., `Bought`, `Sold`, `Div`, `Cash`, `IntInc`, `MiscExp`) |
| date            | Transaction date in `YYYY-MM-DD` format                                     |
| account         | Account name: `VIAC 3a (account number)`                                    |
| security        | Security name (using the newest name for that ISIN)                         |
| optionalSymbol  | ISIN code (using the newest ISIN after mergers)                             |
| shares          | Number of shares/units                                                      |
| price           | Price per share/unit in USD                                                 |
| amount          | Total transaction amount in USD                                             |
| memo            | Additional information (currency conversion, dividend type, etc.)           |
| category        | Expense category (only for commission transactions: `Financial:Financial Advisor`) |

All other columns are left empty as they are not applicable to VIAC transactions.

---

## Currency Conversion

All transactions are converted to USD for tax purposes in Quicken.

### How It Works

- The tool uses the [Frankfurter API](https://www.frankfurter.app/) to retrieve historical exchange rates.
- Exchange rates are fetched for the exact date of each transaction.
- Both the original currency values and exchange rates are preserved in the `memo` field for reference.

### Examples

**CHF Transaction:**
- Original Amount: CHF 25.72
- Exchange rate on transaction date: 1.101900
- Converted: USD 28.340868
- Memo: `CHF-USD (1.101900), Price: 2504.06, Amt: 28.340868`

**USD Transaction:**
- Original Amount: USD 498.60
- Memo: `Already in USD, no currency conversion necessary`

**Zero Amount:**
- Some statements have zero-value transactions (e.g., interest or commission).
- Memo: `Amount is zero, no currency conversion necessary`

### Error Handling

If currency conversion fails:
- **Unsupported currency:** The tool will validate that the currency code is supported before attempting conversion.
- **API unavailable:** If the API is temporarily unavailable, you'll see an error message with details. This is typically an intermittent issue — simply try again later.

---

## How Shares Are Calculated

VIAC statements provide **price** and **amount** for exchange settlements but round the number of shares/units which is inadequate for US taxes.

The tool calculates shares using the formula:

```
shares = amount ÷ price
```

### Example

From the VIAC statement:
- **Price:** CHF 2504.06 per unit
- **Amount:** CHF 25.72

After converting to USD (rate: 1.101900):
- **Price:** USD 2759.22
- **Amount:** USD 28.34
- **Shares:** 28.34 ÷ 2759.22 = 0.01027101862120454331296525829763 units

This calculated value is written to the `shares` column in the output CSV.

### Why This Matters

Quicken requires the number of shares to track tax lots accurately. By calculating shares from the price and amount, the tool ensures:
- Your Quicken portfolio matches your VIAC holdings exactly.
- Capital gains calculations are correct when you sell shares.
- You can track cost basis and holding periods for each purchase.

---

## Security Names and ISINs

The tool always uses the **newest security name and ISIN** for each security across all transactions.

### How Quicken Matches Securities

Quicken matches securities using this logic:
1. **First, match by name** — if the name matches an existing security, use that security (ignore the ISIN).
2. **If no name match, match by ISIN** — if the ISIN matches, use that security.
3. **If neither matches, create a new security.**

**Important:** Quicken never updates the name or ISIN of an existing security. Once set, they remain unchanged.

By using the newest name and ISIN for all transactions, this tool ensures Quicken treats everything as a single security — critical for mergers where both change. See the **Merger Handling** section below for details.

---

## Merger Handling

Security mergers (fund fusions) require careful handling to preserve tax lots — **critical for accurate IRS reporting**.

### The Problem

When VIAC merges two funds (e.g., CSIF Pacific ex Japan → UBS Pacific ex Japan), the security gets a new name **and** new ISIN. If you import transactions with different names and ISINs, Quicken creates **separate securities**, breaking your tax lot history. This causes:

- Incorrect cost basis calculations
- Negative share balances (e.g., if the first transaction for the "new" security is a sell, you'd be selling shares you don't own)
- Invalid capital gains/losses for tax reporting

### How This Tool Handles Mergers

The tool "pre-applies" mergers by updating **all transactions** to use the newest ISIN and security name:

1. **Old ISIN → New ISIN:** Every transaction with the old ISIN is updated to the new ISIN.
2. **Old Name → New Name:** Security names are updated to match the newest name.
3. **No Merger Transaction Created:** The merger itself isn't added as a separate transaction — it's applied behind the scenes.

This makes Quicken treat all transactions (before and after the merger) as belonging to a **single security**, preserving your tax lots.

### Limitation: Only 1:1 Ratios Supported

The tool **only supports 1:1 mergers** (1 old share = 1 new share).

If a non-1:1 merger is detected, you'll see:
```
The conversion ratio 2.5 of the merger in file 'path' is not supported. 
Only a ratio of 1 is supported. See documentation for details.
```

### Critical Warning

When a merger is detected, you'll see this warning:

```
WARNING: Merger detected!
Mergers require special handling to maintain correct tax lots in Quicken.
You must either reimport ALL statements since account inception OR handle the merger manually.
See documentation for detailed instructions.

Do you acknowledge this and wish to continue? (y/n):
```

**Why this matters:** The tool only updates ISINs for transactions in the **current** PDFs. If you previously imported transactions with the old ISIN, those remain unchanged in Quicken, creating new securities.

### Two Solutions

#### Solution 1: Reimport Everything (Recommended)

1. **Reset your VIAC account in Quicken** (delete and recreate, or clear all transactions).
2. **Download ALL VIAC statements** since account inception.
3. **Process all statements** with this tool in one run.
4. **Import the resulting CSV** into Quicken.

Result: All transactions use the newest ISIN/name → single security → tax lots preserved.

#### Solution 2: Manual Merger

If you don't want to reimport everything:

1. **Process statements up to (but not including) the merger date.**
2. **Import into Quicken.**
3. **Manually merge in Quicken.**
4. **Process post-merger statements separately** and import.

Result: Tax lots preserved, but requires manual work in Quicken.

### Example Scenario

**Merger:** CSIF Pacific ex Japan (ISIN 1) → UBS Pacific ex Japan (ISIN 2) on July 1, 2024.

**If you only import July-December 2024:**
- Quicken already has "CSIF Pacific ex Japan (ISIN 1)" from previous imports.
- New transactions import as "UBS Pacific ex Japan (ISIN 2)".
- Quicken creates **a new security** because the security name and ISIN changed.
- Result: Tax lots are split, capital gains calculations are wrong.

**If you reimport all statements (2020-2024):**
- All transactions use "UBS Pacific ex Japan (ISIN 2)".
- Quicken sees one security.
- Result: Tax lots preserved, accurate tax reporting.

---

## Known Limitations

- **Only 1:1 merger ratios are supported.** Non-1:1 mergers must be handled manually in Quicken.
- **Currency conversion requires internet access.** If the Frankfurter API is unavailable, the tool cannot process transactions with non-USD currencies.
- **Each PDF must contain exactly one page.** Multi-page statements will cause an error.

---

## Error Handling

The tool validates all data during parsing. If an issue is detected, you'll see a clear error message indicating:

- The file that caused the error
- What data could not be parsed
- The line or field where the error occurred

### Example Error

```
Parsing Dividend Payment, file: 'C:\statements\2022-06-17_dividend.pdf'.
Could not retrieve Amount from line 'Amount debited: Value date 23.05.2022 CHF 2.13'.
Line must have exactly 3 word(s) but has 7.
```

If you encounter an error:
1. Check that the PDF is a valid VIAC statement (not corrupted or modified).
2. Ensure the PDF has exactly one page.
3. If the error persists, it may indicate a new statement format not yet supported by the tool. Please report the issue.

---

## Example Workflow

1. **Log into VIAC** and download statements from January 2020 to December 2025.
2. **Save all PDFs** to `C:\VIAC_Statements`.
3. **Run ViacQuickenConverter.exe** and enter `C:\VIAC_Statements` when prompted.
4. **Wait for processing** — you'll see progress messages as each file is parsed.
5. **Review the summary:**
   ```
   Parsed Viac Statements:
     Exchange Settlements:               123
     Cancelation Dividend Payments:      5
     Dividend Payments:                  87
     Deposits:                           60
     Reimbursements:                     1
     Interests:                          48
     Commissions:                        24
     Exchange Settlement Fund Fusions:   2
     Total:                              350
   ```
6. **Check for warnings** (e.g., merger detected).
7. **Import the CSV** into Quicken.
8. **Verify in Quicken** that all transactions appear correctly.

---

## FAQ

### Can I process only new statements without reimporting everything?

Yes, as long as:
- No mergers have occurred.
- No security names have changed. (Unless you are okay with the old name being preserved)

Simply process only the new PDFs and import the resulting CSV. Quicken will add the new transactions to your existing account.

### What if I have multiple VIAC portfolios?

The tool supports multiple portfolios. Each portfolio is distinguished by its portfolio number in the account field. For example, `VIAC 3a (3.172.123.119.01)` where `01` at the end is the portfolio number.

### Why are all amounts in USD instead of CHF?

**For US tax purposes, all foreign transactions must be reported in USD.** The IRS requires converting foreign currency amounts using the exchange rate on the transaction date.

By converting to USD during import:
- Quicken calculates cost basis in USD (required for Form 8949).
- Capital gains are computed in USD (required for Schedule D).
- TurboTax import works seamlessly — all amounts are already in the correct currency.

The original currency amounts and exchange rates are preserved in the `memo` field for your records.

### What if the Frankfurter API changes or becomes unavailable?

The tool relies on a free, public API for currency conversion. If it becomes unavailable:
- Try again later (most issues are intermittent).
- If the API is permanently down, the tool would need to be updated to use an alternative data source.

---

## Technical Details

- **Built with:** .NET 10.0
- **PDF Parsing:** UglyToad.PdfPig
- **CSV Writing:** CsvHelper
- **Currency Conversion:** Frankfurter API (free, no API key required)
- **Supported Platforms:** Windows (x86, x64, ARM64), Linux (x64, ARM, ARM64)

---

## Troubleshooting

### "Directory contains no files. Nothing to do."

The folder you specified doesn't contain any PDF files. Make sure:
- You entered the correct folder path.
- The folder contains `.pdf` files (not ZIP files or other formats).

### "PDF file must contain exactly 1 page but contains X page(s)."

VIAC statements should always be single-page PDFs. If you see this error check that the PDF isn't corrupted.

### "Currency 'XXX' is not supported."

The tool encountered a currency code that isn't supported by the Frankfurter API. This is unlikely with VIAC statements, but if it occurs:
- Verify the PDF is a genuine VIAC statement.
- Report the issue if you believe it's an error.

### Quicken doesn't match securities correctly after import

This can happen if:
- You imported transactions with different security names and ISINs before using this tool.
- A merger occurred and you didn't reimport everything.

**Solution:** Delete the affected securities in Quicken and reimport all statements using this tool.

---

## Contributing

Found a bug or have a feature request? Please open an issue on GitHub.

If you encounter a statement format that isn't parsed correctly, please provide:
- The error message
- A sample PDF (with personal information redacted)

---

## License

This project is licensed under the MIT License. See the [LICENSE](./LICENSE) file for details.

---

## Disclaimer

This tool is not affiliated with or endorsed by VIAC or Quicken. Use at your own risk. Always verify the converted data before importing into Quicken.

