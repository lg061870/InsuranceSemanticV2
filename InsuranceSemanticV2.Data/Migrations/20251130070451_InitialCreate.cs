using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceSemanticV2.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Carriers",
                columns: table => new
                {
                    CarrierId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carriers", x => x.CarrierId);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    CompanyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.CompanyId);
                });

            migrationBuilder.CreateTable(
                name: "ContactPolicies",
                columns: table => new
                {
                    ContactPolicyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaxAttemptsPerDay = table.Column<int>(type: "int", nullable: false),
                    AllowedStartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    AllowedEndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactPolicies", x => x.ContactPolicyId);
                });

            migrationBuilder.CreateTable(
                name: "CarrierStateCompliances",
                columns: table => new
                {
                    CarrierStateComplianceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CarrierId = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RuleName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RuleDescription = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarrierStateCompliances", x => x.CarrierStateComplianceId);
                    table.ForeignKey(
                        name: "FK_CarrierStateCompliances_Carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "Carriers",
                        principalColumn: "CarrierId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CarrierId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.ProductId);
                    table.ForeignKey(
                        name: "FK_Products_Carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "Carriers",
                        principalColumn: "CarrierId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    AgentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AvatarUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Specialty = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rating = table.Column<double>(type: "float", nullable: false),
                    Calls = table.Column<int>(type: "int", nullable: false),
                    AvgMinutes = table.Column<int>(type: "int", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    StatusLabel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusColor = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.AgentId);
                    table.ForeignKey(
                        name: "FK_Agents_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductStateAvailabilities",
                columns: table => new
                {
                    AvailabilityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductStateAvailabilities", x => x.AvailabilityId);
                    table.ForeignKey(
                        name: "FK_ProductStateAvailabilities_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentAvailabilities",
                columns: table => new
                {
                    AgentAvailabilityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentAvailabilities", x => x.AgentAvailabilityId);
                    table.ForeignKey(
                        name: "FK_AgentAvailabilities_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "AgentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentCarrierAppointments",
                columns: table => new
                {
                    AppointmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<int>(type: "int", nullable: false),
                    CarrierId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentCarrierAppointments", x => x.AppointmentId);
                    table.ForeignKey(
                        name: "FK_AgentCarrierAppointments_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "AgentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentCarrierAppointments_Carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "Carriers",
                        principalColumn: "CarrierId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentLicenses",
                columns: table => new
                {
                    LicenseId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LicenseNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpiresOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentLicenses", x => x.LicenseId);
                    table.ForeignKey(
                        name: "FK_AgentLicenses_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "AgentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentLogins",
                columns: table => new
                {
                    AgentLoginId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Salt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentLogins", x => x.AgentLoginId);
                    table.ForeignKey(
                        name: "FK_AgentLogins_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "AgentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentSessions",
                columns: table => new
                {
                    SessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSessions", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_AgentSessions_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "AgentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    LeadId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignedAgentId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeadSource = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeadIntent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InterestLevel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QualificationScore = table.Column<int>(type: "int", nullable: true),
                    FollowUpRequired = table.Column<bool>(type: "bit", nullable: true),
                    AppointmentDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeadUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.LeadId);
                    table.ForeignKey(
                        name: "FK_Leads_Agents_AssignedAgentId",
                        column: x => x.AssignedAgentId,
                        principalTable: "Agents",
                        principalColumn: "AgentId");
                });

            migrationBuilder.CreateTable(
                name: "ContactAttempts",
                columns: table => new
                {
                    AttemptId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeadId = table.Column<int>(type: "int", nullable: false),
                    AttemptTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactAttempts", x => x.AttemptId);
                    table.ForeignKey(
                        name: "FK_ContactAttempts_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "LeadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadAppointments",
                columns: table => new
                {
                    AppointmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeadId = table.Column<int>(type: "int", nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadAppointments", x => x.AppointmentId);
                    table.ForeignKey(
                        name: "FK_LeadAppointments_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "LeadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadAuditLogs",
                columns: table => new
                {
                    LeadAuditLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeadId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AgentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadAuditLogs", x => x.LeadAuditLogId);
                    table.ForeignKey(
                        name: "FK_LeadAuditLogs_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "AgentId");
                    table.ForeignKey(
                        name: "FK_LeadAuditLogs_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "LeadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadCallbacks",
                columns: table => new
                {
                    LeadCallbackId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeadId = table.Column<int>(type: "int", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduledTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadCallbacks", x => x.LeadCallbackId);
                    table.ForeignKey(
                        name: "FK_LeadCallbacks_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "LeadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadFollowUps",
                columns: table => new
                {
                    FollowUpId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeadId = table.Column<int>(type: "int", nullable: false),
                    FollowUpDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Result = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadFollowUps", x => x.FollowUpId);
                    table.ForeignKey(
                        name: "FK_LeadFollowUps_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "LeadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadInteractions",
                columns: table => new
                {
                    LeadInteractionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeadId = table.Column<int>(type: "int", nullable: false),
                    AgentId = table.Column<int>(type: "int", nullable: true),
                    Direction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InteractionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadInteractions", x => x.LeadInteractionId);
                    table.ForeignKey(
                        name: "FK_LeadInteractions_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "AgentId");
                    table.ForeignKey(
                        name: "FK_LeadInteractions_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "LeadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadProfiles",
                columns: table => new
                {
                    ProfileId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeadId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadProfiles", x => x.ProfileId);
                    table.ForeignKey(
                        name: "FK_LeadProfiles_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "LeadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadScores",
                columns: table => new
                {
                    LeadScoreId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeadId = table.Column<int>(type: "int", nullable: false),
                    ScoreType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScoreValue = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadScores", x => x.LeadScoreId);
                    table.ForeignKey(
                        name: "FK_LeadScores_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "LeadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadStatusHistories",
                columns: table => new
                {
                    LeadStatusHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeadId = table.Column<int>(type: "int", nullable: false),
                    OldStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByAgentId = table.Column<int>(type: "int", nullable: true),
                    AgentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadStatusHistories", x => x.LeadStatusHistoryId);
                    table.ForeignKey(
                        name: "FK_LeadStatusHistories_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "AgentId");
                    table.ForeignKey(
                        name: "FK_LeadStatusHistories_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "LeadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetsLiabilities",
                columns: table => new
                {
                    AssetsId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    HasHomeEquityValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HomeEquityAmount = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SavingsAmount = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InvestmentsAmount = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetirementAmount = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreditCardDebt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StudentLoans = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AutoLoans = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MortgageDebt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OtherDebt = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetsLiabilities", x => x.AssetsId);
                    table.ForeignKey(
                        name: "FK_AssetsLiabilities_LeadProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "LeadProfiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BeneficiaryInfos",
                columns: table => new
                {
                    BeneficiaryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    BeneficiaryName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeneficiaryRelationship = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeneficiaryDob = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BeneficiaryPercentage = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeneficiaryInfos", x => x.BeneficiaryId);
                    table.ForeignKey(
                        name: "FK_BeneficiaryInfos_LeadProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "LeadProfiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CaliforniaResidents",
                columns: table => new
                {
                    CaliforniaResidentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    ZipCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CcpaAcknowledged = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaliforniaResidents", x => x.CaliforniaResidentId);
                    table.ForeignKey(
                        name: "FK_CaliforniaResidents_LeadProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "LeadProfiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Compliances",
                columns: table => new
                {
                    ComplianceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    TcpaConsent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ZipCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Compliances", x => x.ComplianceId);
                    table.ForeignKey(
                        name: "FK_Compliances_LeadProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "LeadProfiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContactHealths",
                columns: table => new
                {
                    ContactHealthId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CityState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOfBirth = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HospitalizedPast5Years = table.Column<bool>(type: "bit", nullable: false),
                    CurrentlyTakingMedications = table.Column<bool>(type: "bit", nullable: false),
                    Medications = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MedicalConditions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TobaccoUseLast12Months = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactHealths", x => x.ContactHealthId);
                    table.ForeignKey(
                        name: "FK_ContactHealths_LeadProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "LeadProfiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContactInfos",
                columns: table => new
                {
                    ContactInfoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmailAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StreetAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ZipCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactTimeMorning = table.Column<bool>(type: "bit", nullable: false),
                    ContactTimeAfternoon = table.Column<bool>(type: "bit", nullable: false),
                    ContactTimeEvening = table.Column<bool>(type: "bit", nullable: false),
                    ContactTimeAny = table.Column<bool>(type: "bit", nullable: false),
                    ContactMethodPhone = table.Column<bool>(type: "bit", nullable: false),
                    ContactMethodEmail = table.Column<bool>(type: "bit", nullable: false),
                    ContactMethodEither = table.Column<bool>(type: "bit", nullable: false),
                    ConsentContact = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactInfos", x => x.ContactInfoId);
                    table.ForeignKey(
                        name: "FK_ContactInfos_LeadProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "LeadProfiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoverageIntents",
                columns: table => new
                {
                    CoverageIntentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    CoverageType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoverageStartTime = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoverageAmount = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MonthlyBudget = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageIntents", x => x.CoverageIntentId);
                    table.ForeignKey(
                        name: "FK_CoverageIntents_LeadProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "LeadProfiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Dependents",
                columns: table => new
                {
                    DependentsId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    MaritalStatusValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HasDependentsValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NoOfChildren = table.Column<int>(type: "int", nullable: true),
                    AgeRange0To5 = table.Column<bool>(type: "bit", nullable: false),
                    AgeRange6To12 = table.Column<bool>(type: "bit", nullable: false),
                    AgeRange13To17 = table.Column<bool>(type: "bit", nullable: false),
                    AgeRange18To25 = table.Column<bool>(type: "bit", nullable: false),
                    AgeRange25Plus = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dependents", x => x.DependentsId);
                    table.ForeignKey(
                        name: "FK_Dependents_LeadProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "LeadProfiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Employments",
                columns: table => new
                {
                    EmploymentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    EmploymentStatusValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HouseholdIncomeValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Occupation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    YearsEmployedValue = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employments", x => x.EmploymentId);
                    table.ForeignKey(
                        name: "FK_Employments_LeadProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "LeadProfiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HealthInfos",
                columns: table => new
                {
                    HealthInfoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    TobaccoUse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConditionDiabetes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConditionHeart = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConditionBloodPressure = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConditionNone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HealthInsurance = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Height = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Weight = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OverallHealthStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentMedications = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FamilyMedicalHistory = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthInfos", x => x.HealthInfoId);
                    table.ForeignKey(
                        name: "FK_HealthInfos_LeadProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "LeadProfiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InsuranceContexts",
                columns: table => new
                {
                    ContextId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    InsuranceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoverageFor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoverageGoal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InsuranceTarget = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HomeValueString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MortgageBalanceString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MonthlyMortgageString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LoanTermString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EquityString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HasExistingLifeInsuranceString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExistingLifeInsuranceCoverage = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InsuranceContexts", x => x.ContextId);
                    table.ForeignKey(
                        name: "FK_InsuranceContexts_LeadProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "LeadProfiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LifeGoals",
                columns: table => new
                {
                    LifeGoalsId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    ProtectLovedOnesString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayMortgageString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrepareFutureString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PeaceOfMindString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoverExpensesString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnsureString = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LifeGoals", x => x.LifeGoalsId);
                    table.ForeignKey(
                        name: "FK_LifeGoals_LeadProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "LeadProfiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentAvailabilities_AgentId",
                table: "AgentAvailabilities",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentCarrierAppointments_AgentId",
                table: "AgentCarrierAppointments",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentCarrierAppointments_CarrierId",
                table: "AgentCarrierAppointments",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentLicenses_AgentId",
                table: "AgentLicenses",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentLogins_AgentId",
                table: "AgentLogins",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_CompanyId",
                table: "Agents",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_AgentId",
                table: "AgentSessions",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetsLiabilities_ProfileId",
                table: "AssetsLiabilities",
                column: "ProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryInfos_ProfileId",
                table: "BeneficiaryInfos",
                column: "ProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaliforniaResidents_ProfileId",
                table: "CaliforniaResidents",
                column: "ProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarrierStateCompliances_CarrierId",
                table: "CarrierStateCompliances",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_Compliances_ProfileId",
                table: "Compliances",
                column: "ProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContactAttempts_LeadId",
                table: "ContactAttempts",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactHealths_ProfileId",
                table: "ContactHealths",
                column: "ProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContactInfos_ProfileId",
                table: "ContactInfos",
                column: "ProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoverageIntents_ProfileId",
                table: "CoverageIntents",
                column: "ProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dependents_ProfileId",
                table: "Dependents",
                column: "ProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employments_ProfileId",
                table: "Employments",
                column: "ProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HealthInfos_ProfileId",
                table: "HealthInfos",
                column: "ProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InsuranceContexts_ProfileId",
                table: "InsuranceContexts",
                column: "ProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeadAppointments_LeadId",
                table: "LeadAppointments",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadAuditLogs_AgentId",
                table: "LeadAuditLogs",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadAuditLogs_LeadId",
                table: "LeadAuditLogs",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadCallbacks_LeadId",
                table: "LeadCallbacks",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadFollowUps_LeadId",
                table: "LeadFollowUps",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadInteractions_AgentId",
                table: "LeadInteractions",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadInteractions_LeadId",
                table: "LeadInteractions",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadProfiles_LeadId",
                table: "LeadProfiles",
                column: "LeadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_AssignedAgentId",
                table: "Leads",
                column: "AssignedAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadScores_LeadId",
                table: "LeadScores",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadStatusHistories_AgentId",
                table: "LeadStatusHistories",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadStatusHistories_LeadId",
                table: "LeadStatusHistories",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_LifeGoals_ProfileId",
                table: "LifeGoals",
                column: "ProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CarrierId",
                table: "Products",
                column: "CarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductStateAvailabilities_ProductId",
                table: "ProductStateAvailabilities",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentAvailabilities");

            migrationBuilder.DropTable(
                name: "AgentCarrierAppointments");

            migrationBuilder.DropTable(
                name: "AgentLicenses");

            migrationBuilder.DropTable(
                name: "AgentLogins");

            migrationBuilder.DropTable(
                name: "AgentSessions");

            migrationBuilder.DropTable(
                name: "AssetsLiabilities");

            migrationBuilder.DropTable(
                name: "BeneficiaryInfos");

            migrationBuilder.DropTable(
                name: "CaliforniaResidents");

            migrationBuilder.DropTable(
                name: "CarrierStateCompliances");

            migrationBuilder.DropTable(
                name: "Compliances");

            migrationBuilder.DropTable(
                name: "ContactAttempts");

            migrationBuilder.DropTable(
                name: "ContactHealths");

            migrationBuilder.DropTable(
                name: "ContactInfos");

            migrationBuilder.DropTable(
                name: "ContactPolicies");

            migrationBuilder.DropTable(
                name: "CoverageIntents");

            migrationBuilder.DropTable(
                name: "Dependents");

            migrationBuilder.DropTable(
                name: "Employments");

            migrationBuilder.DropTable(
                name: "HealthInfos");

            migrationBuilder.DropTable(
                name: "InsuranceContexts");

            migrationBuilder.DropTable(
                name: "LeadAppointments");

            migrationBuilder.DropTable(
                name: "LeadAuditLogs");

            migrationBuilder.DropTable(
                name: "LeadCallbacks");

            migrationBuilder.DropTable(
                name: "LeadFollowUps");

            migrationBuilder.DropTable(
                name: "LeadInteractions");

            migrationBuilder.DropTable(
                name: "LeadScores");

            migrationBuilder.DropTable(
                name: "LeadStatusHistories");

            migrationBuilder.DropTable(
                name: "LifeGoals");

            migrationBuilder.DropTable(
                name: "ProductStateAvailabilities");

            migrationBuilder.DropTable(
                name: "LeadProfiles");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropTable(
                name: "Carriers");

            migrationBuilder.DropTable(
                name: "Agents");

            migrationBuilder.DropTable(
                name: "Companies");
        }
    }
}
