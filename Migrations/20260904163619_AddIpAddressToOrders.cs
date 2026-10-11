using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace manager_0._1.Migrations
{
    /// <inheritdoc />
    public partial class AddIpAddressToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "Orders",
                type: "TEXT",
                maxLength: 45,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "Orders");
        }
    }
}
