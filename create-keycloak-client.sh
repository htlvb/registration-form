#!/bin/bash

KCADM='docker compose exec app /opt/keycloak/bin/kcadm.sh'

read -p "Keycloak admin user name: " ADMIN_USER_NAME
read -r -s -p "Keycloak admin user password: " ADMIN_USER_PASSWORD
$KCADM config credentials --server http://localhost:8080 --realm master --user $ADMIN_USER_NAME --password "$ADMIN_USER_PASSWORD"

$KCADM create clients --target-realm htlvb \
    --set 'name=HTLVB RegistrationForm' \
    --set clientId=registration-form \
    --set enabled=true \
    --set rootUrl=https://registration.htlvb.at/ \
    --set baseUrl=https://registration.htlvb.at/ \
    --set "redirectUris=[\"http://localhost:5173/*\", \"https://registration.htlvb.at/*\"]" \
    --set "webOrigins=[\"+\"]" \
    --set publicClient=true \
    --set frontchannelLogout=true \
    --set "attributes.\"frontchannel.logout.url\"=https://registration.htlvb.at/" \
    --set "attributes.\"post.logout.redirect.uris\"=http://localhost:5173/##https://registration.htlvb.at/" \
    --set "attributes.\"pkce.code.challenge.method\"=S256"
CLIENT_ID=`$KCADM get clients --target-realm htlvb --fields id,clientId | jq '.[] | select(.clientId == "registration-form") | .id' --raw-output`

$KCADM create clients/$CLIENT_ID/roles --target-realm htlvb --set name=registrationform-eventreader
$KCADM create clients/$CLIENT_ID/roles --target-realm htlvb --set name=registrationform-eventwriter
$KCADM create clients/$CLIENT_ID/roles --target-realm htlvb --set name=registrationform-eventadmin
$KCADM add-roles --target-realm htlvb --gname Lehrer --cclientid registration-form --rolename registrationform-eventreader
$KCADM add-roles --target-realm htlvb --gname Lehrer --cclientid registration-form --rolename registrationform-eventwriter
$KCADM add-roles --target-realm htlvb --uusername eggj --cclientid registration-form --rolename registrationform-eventadmin
$KCADM add-roles --target-realm htlvb --gname Sekretariat --cclientid registration-form --rolename registrationform-eventadmin
