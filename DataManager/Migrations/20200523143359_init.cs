using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DataManager.Migrations
{
    public partial class init : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscribers",
                columns: table => new
                {
                    chatID = table.Column<long>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    mailprocess = table.Column<int>(nullable: false),
                    createDate = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscribers", x => x.chatID);
                });

            migrationBuilder.CreateTable(
                name: "activemails",
                columns: table => new
                {
                    ID = table.Column<long>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    address = table.Column<string>(nullable: true),
                    subscriberchatID = table.Column<long>(nullable: true),
                    endDate = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activemails", x => x.ID);
                    table.ForeignKey(
                        name: "FK_activemails_subscribers_subscriberchatID",
                        column: x => x.subscriberchatID,
                        principalTable: "subscribers",
                        principalColumn: "chatID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "readmails",
                columns: table => new
                {
                    ID = table.Column<long>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    mailID = table.Column<long>(nullable: true),
                    sender = table.Column<string>(nullable: true),
                    title = table.Column<string>(nullable: true),
                    receiveDate = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_readmails", x => x.ID);
                    table.ForeignKey(
                        name: "FK_readmails_activemails_mailID",
                        column: x => x.mailID,
                        principalTable: "activemails",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activemails_subscriberchatID",
                table: "activemails",
                column: "subscriberchatID");

            migrationBuilder.CreateIndex(
                name: "IX_readmails_mailID",
                table: "readmails",
                column: "mailID");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "readmails");

            migrationBuilder.DropTable(
                name: "activemails");

            migrationBuilder.DropTable(
                name: "subscribers");
        }
    }
}
