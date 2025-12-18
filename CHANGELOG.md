# Changelog

All notable changes to this project will be documented in this file.

## [1.0.0] - 2025-12-18

Initial release.

### Features

- Converts VIAC 3a retirement account PDF statements to Quicken-compatible CSV format.
- Processes multiple statement types:
    - **Exchange Settlements**: Buy/sell transactions from fund exchanges.
    - **Dividend Payments**: Income from securities, with automatic currency conversion when needed.
    - **Dividend Cancellations**: Handles cancelled dividend payments.
    - **Deposits**: Contributions to the 3a retirement account.
    - **Interest Payments**: Interest credited to the account.
    - **Commissions**: Fee transactions.
    - **Mergers**: Fund fusions with ISIN updates across all historical transactions.
- Automatically fetches **historical exchange rates** from Frankfurter API for accurate currency conversions.
- Maintains **newest ISIN** across all transactions to prevent duplicate securities in Quicken.
- Maintains **newest security names** to handle rebranding (e.g., Credit Suisse CSIF → UBS funds).
- Special **merger handling** that preserves tax lots by updating ISINs throughout transaction history.
- Validates currency codes against supported currencies before attempting conversions.
- Provides detailed **parsing summary** showing count of each transaction type processed.
- Prevents overwriting previous output files by **auto-incrementing file names**.
- Requires **user confirmation** when mergers are detected, with guidance on proper import workflow.
- Comprehensive **error handling** with context-aware error messages indicating which field failed and why.
