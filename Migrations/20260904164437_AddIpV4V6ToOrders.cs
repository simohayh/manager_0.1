using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace manager_0._1.Migrations
{
    /// <inheritdoc />
    public partial class AddIpV4V6ToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IpAddressV4",
                table: "Orders",
                type: "TEXT",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpAddressV6",
                table: "Orders",
                type: "TEXT",
                maxLength: 45,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IpAddressV4",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IpAddressV6",
                table: "Orders");
        }
    }
}
