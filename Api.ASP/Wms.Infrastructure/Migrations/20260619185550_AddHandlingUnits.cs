using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHandlingUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inventories_ProductId_LocationId_LotId",
                table: "Inventories");

            migrationBuilder.CreateSequence(
                name: "HandlingUnitCodes");

            migrationBuilder.AddColumn<Guid>(
                name: "HandlingUnitId",
                table: "StockOutItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HandlingUnitId",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HandlingUnitId",
                table: "StockInItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HandlingUnitId",
                table: "Inventories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HandlingUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandlingUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandlingUnits_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockOutItems_HandlingUnitId",
                table: "StockOutItems",
                column: "HandlingUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_HandlingUnitId",
                table: "StockMovements",
                column: "HandlingUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_StockInItems_HandlingUnitId",
                table: "StockInItems",
                column: "HandlingUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_HandlingUnitId",
                table: "Inventories",
                column: "HandlingUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_ProductId_LocationId_LotId_HandlingUnitId",
                table: "Inventories",
                columns: new[] { "ProductId", "LocationId", "LotId", "HandlingUnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HandlingUnits_Code",
                table: "HandlingUnits",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_HandlingUnits_LocationId",
                table: "HandlingUnits",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Inventories_HandlingUnits_HandlingUnitId",
                table: "Inventories",
                column: "HandlingUnitId",
                principalTable: "HandlingUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockInItems_HandlingUnits_HandlingUnitId",
                table: "StockInItems",
                column: "HandlingUnitId",
                principalTable: "HandlingUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_HandlingUnits_HandlingUnitId",
                table: "StockMovements",
                column: "HandlingUnitId",
                principalTable: "HandlingUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockOutItems_HandlingUnits_HandlingUnitId",
                table: "StockOutItems",
                column: "HandlingUnitId",
                principalTable: "HandlingUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inventories_HandlingUnits_HandlingUnitId",
                table: "Inventories");

            migrationBuilder.DropForeignKey(
                name: "FK_StockInItems_HandlingUnits_HandlingUnitId",
                table: "StockInItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_HandlingUnits_HandlingUnitId",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StockOutItems_HandlingUnits_HandlingUnitId",
                table: "StockOutItems");

            migrationBuilder.DropTable(
                name: "HandlingUnits");

            migrationBuilder.DropIndex(
                name: "IX_StockOutItems_HandlingUnitId",
                table: "StockOutItems");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_HandlingUnitId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockInItems_HandlingUnitId",
                table: "StockInItems");

            migrationBuilder.DropIndex(
                name: "IX_Inventories_HandlingUnitId",
                table: "Inventories");

            migrationBuilder.DropIndex(
                name: "IX_Inventories_ProductId_LocationId_LotId_HandlingUnitId",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "HandlingUnitId",
                table: "StockOutItems");

            migrationBuilder.DropColumn(
                name: "HandlingUnitId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "HandlingUnitId",
                table: "StockInItems");

            migrationBuilder.DropColumn(
                name: "HandlingUnitId",
                table: "Inventories");

            migrationBuilder.DropSequence(
                name: "HandlingUnitCodes");

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_ProductId_LocationId_LotId",
                table: "Inventories",
                columns: new[] { "ProductId", "LocationId", "LotId" },
                unique: true);
        }
    }
}
