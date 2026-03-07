using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EWMS.Migrations
{
    /// <inheritdoc />
    public partial class TransferLocationUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FromLocationID",
                table: "TransferRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToLocationID",
                table: "TransferRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_FromLocationID",
                table: "TransferRequests",
                column: "FromLocationID");

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_ToLocationID",
                table: "TransferRequests",
                column: "ToLocationID");

            migrationBuilder.AddForeignKey(
                name: "FK_TransferRequests_Locations_FromLocationID",
                table: "TransferRequests",
                column: "FromLocationID",
                principalTable: "Locations",
                principalColumn: "LocationID");

            migrationBuilder.AddForeignKey(
                name: "FK_TransferRequests_Locations_ToLocationID",
                table: "TransferRequests",
                column: "ToLocationID",
                principalTable: "Locations",
                principalColumn: "LocationID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransferRequests_Locations_FromLocationID",
                table: "TransferRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_TransferRequests_Locations_ToLocationID",
                table: "TransferRequests");

            migrationBuilder.DropIndex(
                name: "IX_TransferRequests_FromLocationID",
                table: "TransferRequests");

            migrationBuilder.DropIndex(
                name: "IX_TransferRequests_ToLocationID",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "FromLocationID",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "ToLocationID",
                table: "TransferRequests");
        }
    }
}
