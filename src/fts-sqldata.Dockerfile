## Source: https://github.com/Microsoft/mssql-docker/blob/master/linux/preview/examples/mssql-agent-fts-ha-tools/Dockerfile

# mssql-agent-fts-ha-tools
# Maintainers: Microsoft Corporation (twright-msft on GitHub)
# GitRepo: https://github.com/Microsoft/mssql-docker

# Base OS layer: Latest Ubuntu LTS
FROM ubuntu:16.04

# Install prerequistes since it is needed to get repo config for SQL server
RUN export DEBIAN_FRONTEND=noninteractive
RUN apt-get update
RUN apt-get install -yq curl apt-transport-https

# Get official Microsoft repository configuration
RUN curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add -
RUN curl https://packages.microsoft.com/config/ubuntu/16.04/mssql-server-2019.list | tee /etc/apt/sources.list.d/mssql-server.list
RUN apt-get update
# Install SQL Server from apt
RUN apt-get install -y mssql-server
# Install optional packages
RUN apt-get install -y mssql-server-ha
RUN apt-get install -y mssql-server-fts
# Cleanup the Dockerfile
RUN apt-get clean
RUN rm -rf /var/lib/apt/lists

# Run SQL Server process
CMD /opt/mssql/bin/sqlservr
