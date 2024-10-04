{
  inputs.nixpkgs.url = "github:nixos/nixpkgs/nixos-unstable";
  outputs =
    {
      self,
      nixpkgs,
    }:
    let
      pkgs = nixpkgs.legacyPackages.x86_64-linux;
    in
    {
      packages.x86_64-linux = {
        MatrixContentFilter = pkgs.buildDotnetModule rec {
          pname = "MatrixContentFilter-v${version}";
          version = "1";
          dotnet-sdk = pkgs.dotnet-sdk_8;
          dotnet-runtime = pkgs.dotnet-runtime_8;
          src = ./.;
          projectFile = [
            "MatrixContentFilter/MatrixContentFilter.csproj"
          ];
          nugetDeps = ./MatrixContentFilter/deps.nix;
        };
      };
      modules = {
        default = (
          {
            pkgs,
            lib,
            config,
            ...
          }:
          {
            options.services.MatrixContentFilter = {
              enable = lib.mkEnableOption "MatrixContentFilter";
              accessTokenPath = lib.mkOption {
                type = lib.types.nullOr lib.types.path;
                default = null;
              };
              appSettings = lib.mkOption {
                type = (pkgs.formats.json { }).type;
                default = {
                  "Logging" = {
                    "LogLevel" = {
                      "Default" = "Debug";
                      "System" = "Information";
                      "Microsoft" = "Information";
                    };
                  };
                  "LibMatrixBot" = {
                    "Prefixes" = [
                      "!mcf "
                    ];
                    "MentionPrefix" = false;
                  };
                  "MatrixContentFilter" = { };
                };
              };
            };
            config = {
              assertions = [
                {
                  assertion = config.services.MatrixContentFilter.enable -> config.services.MatrixContentFilter.accessTokenPath != null;
                  message = "MatrixContentFilter: accessTokenPath must be set";
                }
                {
                  # check that appSettings.MatrixContentFilter.Admins exists in the attrset, is not null and has one or more entries
                  assertion =
                    config.services.MatrixContentFilter.enable
                    -> config.services.MatrixContentFilter.appSettings.MatrixContentFilter ? Admins && (lib.lists.length config.services.MatrixContentFilter.appSettings.MatrixContentFilter.Admins) > 0;
                    message = "MatrixContentFilter: appSettings.MatrixContentFilter.Admins must be set";
                }
              ];
              systemd.services = {
                "MatrixContentFilter" = {
                  description = "Rory&::MatrixContentFilter - A Matrix content filtering bot, built for complex communities.";
                  wants = [
                    "network-online.target"
                    "matrix-synapse.service"
                    "conduit.service"
                    "dendrite.service"
                  ];
                  after = [
                    "network-online.target"
                    "matrix-synapse.service"
                    "conduit.service"
                    "dendrite.service"
                  ];
                  wantedBy = [ "multi-user.target" ];
                  serviceConfig = {
                    ExecStart = "${self.packages.x86_64-linux.MatrixContentFilter}/bin/MatrixContentFilter";
                    Restart = "on-failure";
                    RestartSec = "5";
                    DynamicUser = true;
                    WorkingDirectory = "/var/lib/draupnir";
                    StateDirectory = "draupnir";
                    StateDirectoryMode = "0700";
                    ProtectSystem = "strict";
                    ProtectHome = true;
                    PrivateTmp = true;
                    NoNewPrivileges = true;
                    PrivateDevices = true;
                    LoadCredential = [
                      "access_token:${config.services.MatrixContentFilter.accessTokenPath}"
                    ];
                  };

                  environment = {
                    LIBMATRIX_ACCESS_TOKEN_PATH = "/run/credentials/MatrixContentFilter.service/access_token";
                    MATRIXCONTENTFILTER_APPSETTINGS_PATH = (pkgs.formats.json { }).generate "MatrixContentFilter-appsettings.json" (
                      lib.filterAttrsRecursive (_: value: value != null) config.services.MatrixContentFilter.appSettings
                    );
                  };
                };
              };
            };
          }
        );
      };
    };
}
