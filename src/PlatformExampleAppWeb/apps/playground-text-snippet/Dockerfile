# BUILDER START
FROM node:14-alpine AS builder
WORKDIR /usr/src

# https://stackoverflow.com/questions/52196518/could-not-get-uid-gid-when-building-node-docker
# To handle 'not get uid/gid'
RUN npm config set unsafe-perm true
RUN npm install -g @angular/cli
RUN npm install -g @nrwl/cli

# npm install
COPY decorate-angular-cli.js .
COPY package.json .
COPY nx.json .
RUN npm install

# Copy common files
COPY tsconfig.base.json .
COPY .editorconfig .
COPY angular.json .
COPY .eslintrc.json .
COPY .prettierrc .
COPY .prettierignore .
COPY jest.config.ts .
COPY jest.preset.js .
COPY migrations.json .
COPY tools tools
COPY scripts scripts

# Build app files
COPY typings typings
COPY libs libs
COPY apps apps
ARG configuration=development
RUN node --max-old-space-size=4000 ./node_modules/@angular/cli/bin/ng lint platform-core --skip-nx-cache
RUN node --max-old-space-size=4000 ./node_modules/@angular/cli/bin/ng lint apps-domains-text-snippet-domain --skip-nx-cache
RUN node --max-old-space-size=4000 ./node_modules/@angular/cli/bin/ng lint playground-text-snippet --skip-nx-cache
RUN node --max-old-space-size=4000 ./node_modules/@angular/cli/bin/ng build playground-text-snippet --configuration=${configuration} --skip-nx-cache
# BUILDER END

# Build static web app
FROM nginx:1.19.9-alpine as app
EXPOSE 80
RUN apk add --update nodejs npm moreutils
RUN apk --update add bash
RUN apk add dos2unix

RUN rm -rf /usr/share/nginx/html/*
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=builder /usr/src/dist/apps/playground-text-snippet /usr/share/nginx/html
COPY apps/playground-text-snippet/docker-entrypoint.sh /

RUN chmod 777 -R /docker-entrypoint.sh

#Fix error: exec /docker-entrypoint.sh: no such file or directory related to
#window difference in end-of-line (EOL) characters on Windows and Linux. Windows uses CRLF represent the end of a line and Unix/Linux uses LF.
#References: https://stackoverflow.com/questions/53165471/building-docker-images-on-windows-entrypoint-script-no-such-file-or-directory
RUN dos2unix /docker-entrypoint.sh
ENTRYPOINT [ "/docker-entrypoint.sh" ]
