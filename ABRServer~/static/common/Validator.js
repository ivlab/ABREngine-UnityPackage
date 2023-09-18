/* Validator.js
 *
 * Wrapper class for the AJV JSON Schema validator.
 *
 * Copyright (C) 2021, University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

const SCHEMA_URL = 'https://raw.githubusercontent.com/ivlab/abr-schema/master/ABRSchema_0-2-0.json';

export class Validator {
    constructor() {
        this._pendingValidations = [];

        this.schemaID = SCHEMA_URL;
        this._schema = null;

        this._validator = fetch(this.schemaID)
            .then((resp) => resp.json())
            .then((j) => {
                this._schema = j
                let ajv = new Ajv();
                ajv.addSchema(this._schema, this.schemaID);
                console.log(`Using ABR Schema, version ${this._schema.properties.version.default}`)
                return ajv;
            });
    }

    async validate(data) {
        return await this._validator.then((v) => {
            if (!v.validate(this.schemaID, data)) {
                throw this.formatErrors(v.errors);
            } else {
                return data;
            }
        });
    }

    get schema() {
        return this._validator.then((_) => this._schema);
    }

    formatErrors(errors) {
        let fmtErrs = [];
        for (const e of errors) {
            let params = [];
            for (let k in e.params) {
                params.push(`${k}: '${e.params[k]}'`);
            }
            params = params.join(', ');
            fmtErrs.push(`${e.dataPath}: ${e.message} (${params})`);
        }
        return fmtErrs.join(', ');
    }
}