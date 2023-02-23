# BUILDER START
FROM node:14-alpine AS builder
WORKDIR /usr/src

# npm install
COPY package.json .
RUN npm install

# Copy common files
COPY tsconfig.base.json .
COPY nx.json .
COPY angular.json .
COPY .eslintrc.json .
COPY .prettierrc .
COPY decorate-angular-cli.js .
COPY libs libs
COPY typings typings
COPY tools tools

# Build app files
COPY apps/playground-text-snippet apps/playground-text-snippet
ARG configuration=development
RUN npm run ng lint playground-text-snippet
RUN npm run ng build playground-text-snippet -- --configuration=${configuration}
# BUILDER END

# Build static web app
FROM nginx:1.15.1-alpine as app
EXPOSE 80
RUN apk --update add bash && \
    apk add dos2unix

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
