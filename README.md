# PALMS v2 - Pesticide Applicant & Licensing Management System

## Prerequisites
- Node.js (v18 or higher)
- .NET 10.0 SDK
- Microsoft SQL Server (LocalDB or Standard)

## Database Setup & Initialization
1. Open SQL Server Management Studio (SSMS) or `sqlcmd`.
2. Create a new database named `PalmsDb`.
3. Execute the SQL setup files in the `database/` folder sequentially:
   - **`01_schema.sql`** - Builds all underlying tables, keys, and foreign constraints.
   - **`02_seed.sql`** - Populates essential master data, AKC configurations, and default System Staff profiles.
   - **`03_test_data.sql`** - (Optional) Injects pre-populated application flows and mock dummy data for demonstrations.
   - **`drop_constraint.sql`** - Explicit patches applied (execute if you encounter Applicant Profile logic updates).

## Backend Setup (ASP.NET Core API)
1. Navigate to the `Palms.Api/` directory in your terminal.
2. Verify the `ConnectionStrings` section inside `appsettings.json` points directly to your configured SQL instance.
   *(The default string typically looks like: `Server=.;Database=PalmsDb;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False`)*
3. Run the following to build dependencies:
   ```bash
   dotnet restore
   ```
4. Start the backend explicitly:
   ```bash
   dotnet run
   ```
   *The backend will boot up typically on `http://localhost:5246/`.*

## Frontend Setup (Next.js Interface)
1. Navigate to the `palms-web/` directory.
2. Install all frontend dependencies:
   ```bash
   npm install
   ```
3. Boot the development server:
   ```bash
   npm run dev
   ```
   *Access the main portal layout via `http://localhost:3000/`.*

---

## Default System Credentials

### Staff Portals
The default master password securely hashed for all seed accounts is: **`Admin@1234`**

| Role            | Email Address              | Purpose                             |
|-----------------|----------------------------|-------------------------------------|
| System Admin    | `admin@palms.gov.np`       | Configurations, broad oversight   |
| AKC Offical     | `akc.dhanusha@palms.gov.np`| Tier 1 Application checks         |
| PPO Official    | `ppo@palms.gov.np`         | Tier 2 Validations                |
| Chief (Reviewer)| `chief@palms.gov.np`       | Final review and License Generation|

### Applicant Portal Flow
Applicants in this system employ a frictionless, passwordless OTP methodology.
1. Land on `http://localhost:3000/apply/login`.
2. Supply any mobile number initially (for example: `9841000001`).
3. Since real-world SMS/Email gateways mock logic relies on external keys, **the simulated development gateway will physically print your 6-digit OTP passcode over in your .NET Backend terminal log window.** 
4. Punch in that raw `.NET` printed password inside Next.js to gain access to the secure applicant dashboard.
