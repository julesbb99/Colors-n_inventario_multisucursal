using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace colorsin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UsuarioResponsableTransferencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sin `defaultValue: 0`, que es lo que EF genera para rellenar filas
            // existentes: `transferencias` esta vacia, y dejar DEFAULT 0 sobre
            // una columna que es clave foranea sugiere que el cero es un valor
            // valido cuando no lo es (ningun usuario tiene id 0). El modelo
            // tampoco declara ese default, asi que lo dejaria desincronizado.
            //
            // Sobre una tabla con filas habria que decidir primero a que usuario
            // se atribuyen los traslados historicos: con la FK en su sitio,
            // rellenar con cero fallaria.
            migrationBuilder.AddColumn<int>(
                name: "usuario_id",
                table: "transferencias",
                type: "int",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "idx_transf_usuario",
                table: "transferencias",
                column: "usuario_id");

            migrationBuilder.AddForeignKey(
                name: "fk_transf_usuario",
                table: "transferencias",
                column: "usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transf_usuario",
                table: "transferencias");

            migrationBuilder.DropIndex(
                name: "idx_transf_usuario",
                table: "transferencias");

            migrationBuilder.DropColumn(
                name: "usuario_id",
                table: "transferencias");
        }
    }
}
