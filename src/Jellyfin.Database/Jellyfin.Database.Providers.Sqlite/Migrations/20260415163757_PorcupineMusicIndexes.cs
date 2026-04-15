using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Server.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class PorcupineMusicIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserData_UserId",
                table: "UserData");

            migrationBuilder.DropIndex(
                name: "IX_ItemValuesMap_ItemId",
                table: "ItemValuesMap");

            migrationBuilder.CreateIndex(
                name: "IX_UserData_UserId_ItemId",
                table: "UserData",
                columns: new[] { "UserId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserData_UserId_Played_LastPlayedDate",
                table: "UserData",
                columns: new[] { "UserId", "Played", "LastPlayedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemValuesMap_ItemId_ItemValueId",
                table: "ItemValuesMap",
                columns: new[] { "ItemId", "ItemValueId" });

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_TopParentId_Type_SortName",
                table: "BaseItems",
                columns: new[] { "TopParentId", "Type", "SortName" });

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_Type_TopParentId_DateCreated_SortName",
                table: "BaseItems",
                columns: new[] { "Type", "TopParentId", "DateCreated", "SortName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserData_UserId_ItemId",
                table: "UserData");

            migrationBuilder.DropIndex(
                name: "IX_UserData_UserId_Played_LastPlayedDate",
                table: "UserData");

            migrationBuilder.DropIndex(
                name: "IX_ItemValuesMap_ItemId_ItemValueId",
                table: "ItemValuesMap");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_TopParentId_Type_SortName",
                table: "BaseItems");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_Type_TopParentId_DateCreated_SortName",
                table: "BaseItems");

            migrationBuilder.CreateIndex(
                name: "IX_UserData_UserId",
                table: "UserData",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemValuesMap_ItemId",
                table: "ItemValuesMap",
                column: "ItemId");
        }
    }
}
